using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using KargoSim.Cargo;
using KargoSim.Economy;
using UnityEngine;

namespace KargoSim.Mission
{
    /// <summary>
    /// Asama 10 - gorev (mission) sistemi. Oyunun "N kargo teslim et" hedefinin
    /// USTUNE gunluk ekstra hedefler koyar.
    ///
    /// TASARIM KARARLARI
    /// ----------------
    /// 1) MonoBehaviour DEGIL. Saf C# sinif: EditMode testlerinde saniyeler
    ///    icinde, sahne acmadan test edilir.
    /// 2) GameEvents'E DOKUNMAZ. Baska bir ajan GameEvents'i elinde tutuyor;
    ///    bu yuzden bildirim C# event ile (MissionCompleted /
    ///    MissionProgressed) verilir. Statik event kullanmadigimiz icin de
    ///    abonelik sizintisi / domain reload derdi yok.
    /// 3) TANIM ve ILERLEME ayridir. MissionDefinition sadece "ne olcmek"
    ///    der; ilerleme buradaki dizilerde tutulur. Ayni havuz her gun
    ///    yeniden kullanilabilir.
    /// 4) GUN SECIMI DETERMINISTIK. System.Random yerine Mathf.Sin tabanli
    ///    hash kullanilir (CitySetupTool.Hash01 ile ayni formul): ayni gun
    ///    tohumu her zaman ayni gorevleri verir, "bugun ne cikti?" sorusu
    ///    kaydedilip tekrar oynanabilir.
    /// 5) Girdi listesi DEGISTIRILMEZ. Gelen MissionDefinition nesneleri
    ///    (ScriptableObject listesi olabilir) oyuncu verisi sayilir; sadece
    ///    guvenli ozelliklerinden okunur.
    /// </summary>
    public sealed class MissionSystem
    {
        /// <summary>Bos gun yerine kullanilan paylasilabilir dizi (allocation yok).</summary>
        private static readonly MissionDefinition[] NoMissions = new MissionDefinition[0];

        // --- Tanim havuzu (degistirilmez) ---
        private readonly IReadOnlyList<MissionDefinition> _pool;
        private readonly EconomyConfig _economy;

        // --- Bugunun secilen gorevleri (BeginDay ile degisir) ---
        private MissionDefinition[] _active = NoMissions;
        private ReadOnlyCollection<MissionDefinition> _activeView =
            new ReadOnlyCollection<MissionDefinition>(NoMissions);

        // --- Ilerleme (aktif gorevlerle ayni indeks) ---
        private int[] _progress = new int[0];
        private bool[] _completed = new bool[0];
        private bool[] _broken = new bool[0];

        // --- Gun toplamlari ---
        private int _day = 1;
        private bool _dayOpen = true;
        private int _failures;
        private int _onTime;
        private int _onTimeTotal;

        /// <summary>Gorev tamamlandi. Parametre: gorevin indeksi (0..ActiveCount-1).</summary>
        public event Action<int> MissionCompleted;

        /// <summary> gorev ilerledi. Parametreler: (indeks, 0..1 oran).</summary>
        public event Action<int, float> MissionProgressed;

        /// <summary>
        /// <paramref name="defs"/> gorev HAVUZUDUR; gunde 1-2 tanesi secilir.
        /// <paramref name="economy"/> odulun ekonomiyle baglanti kurmasi icin
        /// saklanir (ayarlar ileride odul carpani olarak kullanilabilir);
        /// null olabilir ve sistem calismaya devam eder.
        ///
        /// Kurucu gun 1'i baslatir: sistem her an kullanilabilir durumdadir,
        /// BeginDay cagrilmadan da IsComplete/Progress01 sorulabilir.
        /// </summary>
        public MissionSystem(IReadOnlyList<MissionDefinition> defs, EconomyConfig economy)
        {
            _economy = economy;
            _pool = SanitizePool(defs);
            BeginDay(1);
        }

        // ------------------------------------------------------------------
        // Okuma
        // ------------------------------------------------------------------

        /// <summary>Bugunun secilen gorevleri (indeksler bununla eslesir).</summary>
        public IReadOnlyList<MissionDefinition> Missions => _activeView;

        /// <summary>Secim havuzu (constructor'a giren temizlenmis kopya).</summary>
        public IReadOnlyList<MissionDefinition> Pool => _pool;

        /// <summary>Ekonomi ayarlari (null olabilir).</summary>
        public EconomyConfig Economy => _economy;

        /// <summary>Bugun kac gorev secildi (0-2).</summary>
        public int ActiveCount => _active.Length;

        /// <summary>Simdaki gun (1'den baslar).</summary>
        public int CurrentDay => _day;

        /// <summary>Gun hala acik mi? Kapatilinca (EndDay) Report yok sayilir.</summary>
        public bool IsDayOpen => _dayOpen;

        /// <summary>Bu gun basarisiz teslimat sayisi (NoFailures icin).</summary>
        public int FailureCount => _failures;

        /// <summary>Zamaninda teslim orani (0-100, yuvarlanmis).</summary>
        public int OnTimePercent => _onTimeTotal > 0
            ? Mathf.RoundToInt(100f * _onTime / _onTimeTotal)
            : 0;

        /// <summary>Toplam odul: YALNIZCA tamamlanan gorevlerin odulu.</summary>
        public int TotalRewardMoney
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _active.Length; i++)
                    if (_completed[i]) sum += _active[i].SafeRewardMoney;
                return sum;
            }
        }

        /// <summary>Toplam odul: YALNIZCA tamamlanan gorevlerin XP'si.</summary>
        public int TotalRewardXp
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _active.Length; i++)
                    if (_completed[i]) sum += _active[i].SafeRewardXp;
                return sum;
            }
        }

        /// <summary>
        /// requiresAll isaretli gorevlerin HEPSI bittiyse true.
        ///
        /// Boyle bir gorev yoksa true doner: "tumu bitti" bonusu, o gunku
        /// gorevler icin zorunlu kisi degilse her zaman verilir.
        /// </summary>
        public bool AllCompleted
        {
            get
            {
                for (int i = 0; i < _active.Length; i++)
                    if (_active[i].requiresAll && !_completed[i]) return false;
                return true;
            }
        }

        /// <summary>Bugun butun gorevler bitti mi? (sart olmayan bonus dahil)</summary>
        public bool EveryMissionCompleted
        {
            get
            {
                for (int i = 0; i < _active.Length; i++)
                    if (!_completed[i]) return false;
                return true;
            }
        }

        /// <summary>Gorev tamamlandi mi? (gecersiz indeks -> false)</summary>
        public bool IsComplete(int index)
        {
            if (index < 0 || index >= _active.Length) return false;
            return _completed[index];
        }

        /// <summary>
        /// Gorev kalici olarak bozuldu mu? (NoFailures: bir basarisiz teslimat
        /// gormus demektir; bu gun duzelmez.)
        /// </summary>
        public bool IsBroken(int index)
        {
            if (index < 0 || index >= _active.Length) return false;
            return _broken[index];
        }

        /// <summary>Ham ilerleme degeri (ornegin 3/8 icin 3).</summary>
        public int CurrentValue(int index)
        {
            if (index < 0 || index >= _active.Length) return 0;
            return _progress[index];
        }

        /// <summary>Hedef deger (ornegin 3/8 icin 8).</summary>
        public int TargetValue(int index)
        {
            if (index < 0 || index >= _active.Length) return 0;
            return _active[index].SafeTargetValue;
        }

        /// <summary>Ilerleme orani 0..1. HUD'daki cubuk bunu okur.</summary>
        public float Progress01(int index)
        {
            if (index < 0 || index >= _active.Length) return 0f;
            return Ratio01(index);
        }

        /// <summary>Tamamlanma bayraklarinin ANLIK KOPYASI (guvenli dis nokta).</summary>
        public IReadOnlyList<bool> CompletedFlags => Array.AsReadOnly((bool[])_completed.Clone());

        // ------------------------------------------------------------------
        // Gun yasam dongusu
        // ------------------------------------------------------------------

        /// <summary>
        /// Yeni gun: gorevler gun tohumuyla TEKRAR secilir, ilerleme ve
        /// bozulma sifirlanir, gun toplamlari temizlenir, gun acilir.
        /// </summary>
        public void BeginDay(int day)
        {
            _day = day < 1 ? 1 : day;
            _dayOpen = true;
            _failures = 0;
            _onTime = 0;
            _onTimeTotal = 0;

            SelectMissions(_day);
        }

        /// <summary>
        /// Gun sonu kapanisi. Gun boyunca olcumlenemeyen turler burada
        /// karara baglanir: NoFailures (gun temiz mi?) ve OnTimeRate.
        ///
        /// Kapanista yeni Report kabul edilmez; ertesi gun BeginDay ile acilir.
        /// </summary>
        public void EndDay()
        {
            if (!_dayOpen) return;
            _dayOpen = false;

            for (int i = 0; i < _active.Length; i++)
            {
                if (_completed[i] || _broken[i]) continue;

                switch (_active[i].kind)
                {
                    case MissionKind.NoFailures:
                        if (_failures == 0) ForceComplete(i);
                        break;

                    case MissionKind.OnTimeRate:
                        if (OnTimePercent >= _active[i].SafeTargetValue) ForceComplete(i);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------
        // Ilerleme bildirimi
        // ------------------------------------------------------------------

        /// <summary>
        /// Oyunun bu gunki durumunu bildirir.
        /// </summary>
        /// <param name="kind">Hangi olcum ilerleyecek?</param>
        /// <param name="value">Deger: sayac icin kargo adedi, EarnMoney icin
        /// TL, ReachLevel icin seviye, OnTimeRate icin 1.</param>
        /// <param name="success">Islem basarili mi? false ise NoFailures
        /// gorevleri kalici olarak bozulur ve sayac turleri artmaz.</param>
        /// <remarks>
        /// NoFailures turu turun kendisiyle degil, TUM basarisiz teslimatlarla
        /// ilgilenir; OnTimeRate turu icin her teslimat icin (basarili = zamaninda)
        /// bir kez cagrilir.
        /// </remarks>
        public void Report(MissionKind kind, int value, bool success)
        {
            ReportInternal(kind, value, success, null);
        }

        /// <summary>
        /// Oncelik filtreli bildirim. DeliverPriority gorevleri YALNIZCA
        /// requiredPriority degeri esitse ilerler; diger turlerde deger
        /// yoksayilir.
        /// </summary>
        public void Report(MissionKind kind, int value, bool success, ParcelPriority priority)
        {
            ReportInternal(kind, value, success, priority);
        }

        private void ReportInternal(MissionKind kind, int value, bool success,
                                    ParcelPriority? priorityFilter)
        {
            if (!_dayOpen) return;

            // Gun toplamlari HER zaman guncellenir: bunlar bugunun istatistigi
            // (gorev secimi degisse de dogru kalmali).
            if (!success) _failures++;
            if (kind == MissionKind.OnTimeRate)
            {
                _onTimeTotal++;
                if (success) _onTime++;
            }

            // NoFailures turun KENDISIyle iliskisi yoktur: gelen olayin turu ne
            // olursa olsun tek bir basarisiz teslimat onu bozar. Bu yuzden
            // asagidaki tur filtresinden ONCE ele alinir.
            if (!success)
            {
                for (int i = 0; i < _active.Length; i++)
                    if (_active[i].kind == MissionKind.NoFailures) BreakMission(i);
            }

            if (value < 0) value = 0;

            for (int i = 0; i < _active.Length; i++)
            {
                MissionDefinition m = _active[i];
                if (m.kind != kind) continue;
                if (_completed[i] || _broken[i]) continue;
                if (!PriorityMatches(m, priorityFilter)) continue;

                switch (m.kind)
                {
                    case MissionKind.OnTimeRate:
                        // Gun kalitesi olcumudur: gun boyunca oran canli
                        // gosterilir, karar gun sonunda verilir. (Tek bir
                        // teslimatta oran %100 olurdu; erken kazandirmak
                        // hem anlamsiz hem de haksiz olurdu.)
                        if (!success) break;
                        if (_onTimeTotal > 0) UpdateProgressOnly(i, OnTimePercent);
                        break;

                    case MissionKind.ReachLevel:
                        // Seviye kumulatif DEGIL, en yuksek seviye saklanir
                        // (3 sonra 2 gelirse ilerleme 3'te kalir).
                        if (!success) break;
                        if (value > _progress[i]) SetProgress(i, value);
                        break;

                    case MissionKind.NoFailures:
                        // Yanlislikla NoFailures turuyle Report gelirse
                        // sayac ilerlememeli (yukarida zaten ele alindi).
                        break;

                    default:
                        // DeliverCount, EarnMoney, DeliverPriority: kumulatif.
                        if (!success) break;
                        if (value > 0) SetProgress(i, _progress[i] + value);
                        break;
                }
            }
        }

        // ------------------------------------------------------------------
        // Ic mantik
        // ------------------------------------------------------------------

        private static bool PriorityMatches(MissionDefinition m, ParcelPriority? filter)
        {
            if (!m.FiltersPriority) return true;
            if (!filter.HasValue) return true;   // filtre yok: cagiran zaten ayirmis
            return filter.Value == m.requiredPriority;
        }

        /// <summary>Sayaci yazar, oran degistiyse olay yayar, hedef dolduysa tamamlar.</summary>
        private void SetProgress(int index, int value)
        {
            float before = Ratio01(index);
            _progress[index] = value;
            float after = Ratio01(index);

            if (Mathf.Abs(after - before) > 0.0001f)
                MissionProgressed?.Invoke(index, after);

            if (value >= _active[index].SafeTargetValue)
                ForceComplete(index);
        }

        /// <summary>
        /// Yalnizca gostergeci ilerleme: sayaci yazar, olay yayar ama
        /// TAMAMLAMAZ (gun sonunda karara baglanan turler icin).
        /// </summary>
        private void UpdateProgressOnly(int index, int value)
        {
            float before = Ratio01(index);
            _progress[index] = value;
            float after = Ratio01(index);

            if (Mathf.Abs(after - before) > 0.0001f)
                MissionProgressed?.Invoke(index, after);
        }

        /// <summary>Kalici bozulma: ilerleme sifirlanir, gune kadar duzelmez.</summary>
        private void BreakMission(int index)
        {
            if (_broken[index]) return;

            _broken[index] = true;
            _completed[index] = false;
            _progress[index] = 0;
            MissionProgressed?.Invoke(index, 0f);
        }

        /// <summary>Gun sonu kapanisi: hedef zaten dolmus sayilir.</summary>
        private void ForceComplete(int index)
        {
            if (_completed[index]) return;

            _completed[index] = true;
            if (_progress[index] < _active[index].SafeTargetValue)
                _progress[index] = _active[index].SafeTargetValue;

            MissionCompleted?.Invoke(index);
        }

        private float Ratio01(int index)
        {
            int target = _active[index].SafeTargetValue;
            if (target <= 0) return _completed[index] ? 1f : 0f;
            return Mathf.Clamp01((float)_progress[index] / target);
        }

        // ------------------------------------------------------------------
        // Deterministik gun secimi (CitySetupTool.Hash01 ile ayni formul)
        // ------------------------------------------------------------------

        /// <summary>
        /// Deterministik seudo-rastgele [0,1).
        ///
        /// Mathf.Sin tabanli klasik GLSL hash. System.Random kullanmamizin
        /// sebebi: tohum verilse de platformlar arasinda ayni sonucu
        /// vermez; bu formul her platformda sabit kalir.
        /// </summary>
        private static float Hash01(int seed)
        {
            float v = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        private void SelectMissions(int day)
        {
            int wanted = PickMissionCount(day);
            var picked = new List<MissionDefinition>(wanted);
            var used = new List<int>(wanted);

            for (int slot = 0; slot < wanted; slot++)
            {
                int index = PickPoolIndex(day, slot, used);
                if (index < 0) break;
                used.Add(index);
                picked.Add(_pool[index]);
            }

            _active = picked.ToArray();
            _activeView = Array.AsReadOnly(_active);

            int count = _active.Length;
            _progress = new int[count];
            _completed = new bool[count];
            _broken = new bool[count];

            // NoFailures "sifir basarisiz" hedefidir: gun temiz basladigi icin
            // sayac 1'den baslar. Boyle HUD'da "0 basarisiz" 0% gibi
            // gorunmez, cubuk dolu cikar.
            for (int i = 0; i < count; i++)
                if (_active[i].kind == MissionKind.NoFailures) _progress[i] = 1;
        }

        /// <summary>Gunde 1-2 gorev: havuzda tek tanim varsa zorla 1.</summary>
        private int PickMissionCount(int day)
        {
            if (_pool.Count <= 1) return _pool.Count;
            return Hash01(day * 977 + 5) < 0.5f ? 1 : 2;
        }

        /// <summary>Ayni gorevi iki kez secmemize izin vermeyen indeks.</summary>
        private int PickPoolIndex(int day, int slot, List<int> used)
        {
            if (used.Count >= _pool.Count) return -1;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                int salt = day * 131 + slot * 17 + attempt * 7919;
                int index = Mathf.FloorToInt(Hash01(salt) * _pool.Count);
                index = Mathf.Clamp(index, 0, _pool.Count - 1);
                if (!used.Contains(index)) return index;
            }

            // Hash (deger sinirina dayanarak) hep ayni indeksi verirse
            // sirayla ilerleyerek deterministik bir yedek bul.
            for (int offset = 0; offset < _pool.Count; offset++)
            {
                int index = (slot + offset) % _pool.Count;
                if (!used.Contains(index)) return index;
            }

            return -1;
        }

        /// <summary>
        /// Gelen listeyi temiz bir kopya yapar: null girisler dusurulur
        /// (havuzun tamami null olabilir), AKIYCI LISTESI DEGISTIRILMEZ.
        /// </summary>
        private static IReadOnlyList<MissionDefinition> SanitizePool(
            IReadOnlyList<MissionDefinition> defs)
        {
            if (defs == null || defs.Count == 0) return NoMissions;

            var clean = new List<MissionDefinition>(defs.Count);
            for (int i = 0; i < defs.Count; i++)
            {
                MissionDefinition m = defs[i];
                if (m == null) continue;
                clean.Add(m);
            }

            return clean.Count == 0 ? NoMissions : new ReadOnlyCollection<MissionDefinition>(clean);
        }
    }
}
