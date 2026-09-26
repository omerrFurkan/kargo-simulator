using System;
using System.Collections.Generic;
using UnityEngine;

namespace KargoSim.NPC
{
    /// <summary>
    /// Bir adresin MÜŞTERISI: statik bilgi.
    ///
    /// Neden ScriptableObject DEGIL: brief'te "her NPC icin gereksiz
    /// ScriptableObject uretme" deniyor ve bu projenin veri mimarisi
    /// bunu zaten gosteriyor - adresler `MapLayout` (TEK ScriptableObject)
    /// icinde. NPC de adresin bir parcasidir; ayri bir SO her adres icin
    /// (14 tane) dosya ve menu demekti, kazanci yoktu.
    ///
    /// Ayrica NPC verisi DERLEME ZAMANINDA degismez, sadece gun degisir.
    /// Gunluk degisen sey zaten runtime durumdur (`NpcState`).
    /// </summary>
    [Serializable]
    public sealed class NpcProfile
    {
        [Tooltip("Adres kimligi (DeliveryStop.DestinationId ile ayni).")]
        public int destinationId;

        [Tooltip("Musterinin adi. Stok ad listesinden uretilir.")]
        public string name = string.Empty;

        [Tooltip("Bu adres baslangicta bos mu? (MVC ilkesi)")]
        public bool homeByDefault = true;

        /// <summary>
        /// Kapida karsi karsiya gelme olasiligi. 0 = hic gelmez,
        /// 1 = her zaman gelir.
        ///
        /// Neden olasilik: NPC'nin her gun her zaman evde olmasi oyunu
        /// tek turlu yapar. Olmak/olmamak, kuryenin o gunku kargo
        /// sayisina gore degismeli.
        /// </summary>
        [Range(0f, 1f)] public float homeChance = 0.75f;

        public NpcProfile(int destinationId, string name, bool homeByDefault, float homeChance)
        {
            this.destinationId = destinationId;
            this.name = name;
            this.homeByDefault = homeByDefault;
            this.homeChance = Mathf.Clamp01(homeChance);
        }
    }

    /// <summary>
    /// NPC'nin o ANKI durumu. Statik profil DEGIL - gundegismez.
    /// </summary>
    public enum NpcState
    {
        /// <summary>Bugun adreste kimse yok.</summary>
        NotHome = 0,

        /// <summary>Evde, kapiyi acmaya hazir.</summary>
        HomeIdle = 1,

        /// <summary>Kapida, kodu soylemeyi bekliyor.</summary>
        Answered = 2,

        /// <summary>Konusma bitti (kod dogrulandi veya vazgecildi).</summary>
        ConversationClosed = 3,
    }

    /// <summary>
    /// Adres basina NPC runtime durumu.
    /// </summary>
    public sealed class NpcRuntime
    {
        public NpcProfile Profile { get; }

        public NpcState State { get; internal set; }

        /// <summary>Konusmada ele alinan kargo kimligi (yoksa 0).</summary>
        public int PendingParcelId { get; internal set; }

        public NpcRuntime(NpcProfile profile)
        {
            Profile = profile;
            State = NpcState.NotHome;
        }

        public bool IsHome => State != NpcState.NotHome;

        public string Name => Profile != null ? Profile.name : "Musteri";
    }

    /// <summary>
    /// NPC sistemi: kimin bugun evde oldugunu yonetir.
    ///
    /// MONOBEHAVIOUR DEGIL, statik event KULLANMAZ. Saf C# ve C# event:
    /// test edilebilir ve yikimda sizinti olmaz.
    ///
    /// DETERMINIZM: kimin evde oldugu gun tohumundan turetilir. Ayni gun
    /// iki kez soruldugunda ayni cevap verir - testler kararli calisir.
    ///
    /// ILERIDE: `homeChance` yerine saat/takvim kurali eklenebilir. Buradaki
    /// API (IsHome, AnswerDoor, CloseConversation) o degisikligi hic
    /// etkilemez; yalnizca `ResolveHomeState` icerigi degisir.
    /// </summary>
    public sealed class NPCSystem
    {
        private readonly Dictionary<int, NpcRuntime> _byDestination =
            new Dictionary<int, NpcRuntime>();

        private readonly List<NpcRuntime> _all = new List<NpcRuntime>();

        private int _day = 1;

        /// <summary>Kac adres kayitli?</summary>
        public int Count => _all.Count;

        /// <summary>Bu gun yapilan kapı ziyareti sayisi (istatistik).</summary>
        public int AnsweredCount { get; private set; }

        /// <summary>Bu gun "kapi bos bulundu" sayisi.</summary>
        public int NotHomeCount { get; private set; }

        /// <summary>Adres listesinden profilleri kurar.</summary>
        /// <param name="destinationIds">Adres kimlikleri.</param>
        /// <param name="names">Adlara eslesen isimler (null olabilir).</param>
        /// <param name="homeChance">Varsayilan evde olma sansi.</param>
        public void Build(IReadOnlyList<int> destinationIds, IReadOnlyList<string> names,
                          float homeChance)
        {
            _byDestination.Clear();
            _all.Clear();

            if (destinationIds == null) return;

            for (int i = 0; i < destinationIds.Count; i++)
            {
                int id = destinationIds[i];
                if (_byDestination.ContainsKey(id)) continue;

                string npcName = (names != null && i < names.Count && !string.IsNullOrEmpty(names[i]))
                    ? names[i]
                    : "Musteri " + (i + 1);

                // Adrenin indeksi evde olma sansini biraz degistirir:
                // her adres ayni olasilikta olursa bir gun 14 kişi de
                // evde, bir gun hicbiri evde olabiliyor. Kademeli dagilim
                // gunluk tabii his veriyor.
                float chance = Mathf.Clamp01(homeChance - i * 0.02f + 0.05f);

                var profile = new NpcProfile(id, npcName, true, chance);
                var runtime = new NpcRuntime(profile);

                _byDestination.Add(id, runtime);
                _all.Add(runtime);
            }
        }

        /// <summary>
        /// Yeni gun: kimlerin evde oldugunu yeniden ceker ve tum
        /// konusmalari kapatir.
        /// </summary>
        public void BeginDay(int day)
        {
            _day = day < 1 ? 1 : day;
            AnsweredCount = 0;
            NotHomeCount = 0;

            for (int i = 0; i < _all.Count; i++)
            {
                NpcRuntime npc = _all[i];
                npc.PendingParcelId = 0;
                npc.State = ResolveHomeState(npc) ? NpcState.HomeIdle : NpcState.NotHome;
            }
        }

        /// <summary>
        /// Bu adres icin NPC var mi?
        /// </summary>
        public bool HasNpc(int destinationId) => _byDestination.ContainsKey(destinationId);

        /// <summary>Adresin NPC'sini dondurur (yoksa null).</summary>
        public NpcRuntime Get(int destinationId)
        {
            NpcRuntime npc;
            return _byDestination.TryGetValue(destinationId, out npc) ? npc : null;
        }

        /// <summary>Bugun bu adreste musteri var mi?</summary>
        public bool IsHome(int destinationId)
        {
            NpcRuntime npc = Get(destinationId);
            return npc != null && npc.IsHome;
        }

        /// <summary>
        /// Kapi calindi: NPC kapiyi acar ve kodu soylemeye hazir olur.
        /// Evde degilse false doner.
        /// </summary>
        public bool AnswerDoor(int destinationId)
        {
            NpcRuntime npc = Get(destinationId);
            if (npc == null) return false;

            if (!npc.IsHome)
            {
                npc.State = NpcState.NotHome;
                NotHomeCount++;
                return false;
            }

            npc.State = NpcState.Answered;
            AnsweredCount++;
            return true;
        }

        /// <summary>Konusmayi kapatir (kod dogrulandi veya oyuncu vazgecti).</summary>
        public void CloseConversation(int destinationId)
        {
            NpcRuntime npc = Get(destinationId);
            if (npc == null) return;

            npc.PendingParcelId = 0;
            npc.State = npc.IsHome ? NpcState.HomeIdle : NpcState.NotHome;
        }

        /// <summary>
        /// Kapiyi calan musteri bu kargoyu bekliyor mu?
        ///
        /// Yanlis kargo ile kapi acilmaz: oyuncu yanlis kutuyu eline
        /// alip dogru kapinin onune giderse once dogru kargoyu bulmasi
        /// gerekir.
        /// </summary>
        public bool AcceptsParcel(int destinationId, int parcelId)
        {
            NpcRuntime npc = Get(destinationId);
            if (npc == null) return false;
            if (npc.State != NpcState.Answered) return false;
            return parcelId > 0;
        }

        /// <summary>
        /// Gun tohumu + adres kimliginden deterministik "evde mi" karari.
        ///
        /// Mathf.Sin tabanli seudo-rastgele (projede CitySetupTool ile ayni
        /// desen) - System.Random ve UnityEngine.Random kullanilmiyor, boylece
        /// testler tekrarlanabilir.
        /// </summary>
        private bool ResolveHomeState(NpcRuntime npc)
        {
            if (npc.Profile == null) return false;

            float t = _day * 0.6180339887f + npc.Profile.destinationId * 0.7548776662f;
            float r = Frac(Mathf.Sin(t) * 43758.5453f);

            return r < npc.Profile.homeChance;
        }

        private static float Frac(float v) => v - Mathf.Floor(v);
    }
}
