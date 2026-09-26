using System;
using KargoSim.Cargo;

namespace KargoSim.Mission
{
    /// <summary>
    /// Gorevin olcdugu sey: "ne" olculur.
    ///
    /// Yeni bir gorev turu eklemek icin sadece buraya bir enum degeri eklemek
    /// yeterlidir; MissionSystem her turu kendi kuraliyla isler.
    /// </summary>
    public enum MissionKind
    {
        /// <summary>N kargo teslim et (basarili sayilir).</summary>
        DeliverCount = 0,

        /// <summary>Gun icinde X TL kazanc yap.</summary>
        EarnMoney = 1,

        /// <summary>Gun sonunda X. seviyeye ulas.</summary>
        ReachLevel = 2,

        /// <summary>requiredPriority onceliginde N kargo teslim et.</summary>
        DeliverPriority = 3,

        /// <summary>Gun boyunca hicbir teslimat basarisiz olmasin.</summary>
        NoFailures = 4,

        /// <summary>Teslimatlarin en az %X'i zamaninda olsun.</summary>
        OnTimeRate = 5,
    }

    /// <summary>
    /// Tek bir gorevin TANIMI (veri). Ilerleme burada tutulmaz; ilerlemeyi
    /// MissionSystem yonetir, boylece ayni tanim her gun farkli ilerlemeyle
    /// yeniden kullanilabilir.
    ///
    /// [Serializable] + public alanlar: ileride bir ScriptableObject icinde
    /// liste olarak saklanabilir (Unity serilestirmesi public alan ister).
    /// </summary>
    [Serializable]
    public sealed class MissionDefinition
    {
        /// <summary>Tekil kod. Ayni tanimlar arasinda ayirt etmek icin kullanilir.</summary>
        public string id = "";

        /// <summary>KISA baslik (HUD'da gorunur).</summary>
        public string title = "";

        /// <summary>Uzun aciklama (gun basi panosu / ayrinti ekrani).</summary>
        public string description = "";

        /// <summary>Hangi olcume gore ilerlenecek?</summary>
        public MissionKind kind;

        /// <summary>Hedef deger. NoFailures haricinde her tur icin anlamlidir.</summary>
        public int targetValue = 1;

        /// <summary>Yalnizca DeliverPriority turu icin kullanilir.</summary>
        public ParcelPriority requiredPriority = ParcelPriority.Normal;

        /// <summary>Tamamlaninca odenen para (TL).</summary>
        public int rewardMoney;

        /// <summary>Tamamlaninca kazanilan XP.</summary>
        public int rewardXp;

        /// <summary>
        /// Bu gorev, gunkun TUM gorevlerinin bitmesini bekleyen bonus gorevi mi?
        /// MissionSystem.AllCompleted yalnizca bunlari sayar.
        /// </summary>
        public bool requiresAll;

        /// <summary>Unity/serializer icin bos kurucu.</summary>
        public MissionDefinition()
        {
        }

        /// <summary>Hepsini tek satirda kurar (elle doldurma zahmetini kaldirir).</summary>
        public MissionDefinition(MissionKind kind, int target, string title, int money, int xp)
        {
            Init(kind, target, title, money, xp);
        }

        /// <summary>
        /// Kisa kurgu: once Create ile uret, sonra istenen alanlari degistir.
        /// (ornegin requiredPriority veya requiresAll).
        /// </summary>
        public static MissionDefinition Create(MissionKind kind, int target, string title,
                                              int money, int xp)
        {
            var m = new MissionDefinition();
            m.Init(kind, target, title, money, xp);
            return m;
        }

        /// <summary>Oncelik gerektiren gorev (ornegin yalnizca Acil kargo).</summary>
        public static MissionDefinition CreatePriority(MissionKind kind, int target, string title,
                                                       ParcelPriority priority, int money, int xp)
        {
            MissionDefinition m = Create(kind, target, title, money, xp);
            m.requiredPriority = priority;
            return m;
        }

        /// <summary>Gunun butun gorevleriyle birlikte tamamlanmasi gereken bonus.</summary>
        public static MissionDefinition CreateBonus(MissionKind kind, int target, string title,
                                                   int money, int xp)
        {
            MissionDefinition m = Create(kind, target, title, money, xp);
            m.requiresAll = true;
            return m;
        }

        private void Init(MissionKind kind, int target, string title, int money, int xp)
        {
            this.kind = kind;
            targetValue = target;
            this.title = title ?? "";
            rewardMoney = money;
            rewardXp = xp;
            requiredPriority = ParcelPriority.Normal;
            requiresAll = false;

            Sanitize();

            if (id.Length == 0) id = BuildId(this.kind, this.title, this.targetValue);
            description = DefaultDescription(this.kind, this.targetValue, this.requiredPriority);
        }

        /// <summary>
        /// Bozuk / eksik veriyi guvenli araliga getirir.
        ///
        /// Neden gerekli: bu sinif [Serializable], yani degerler Inspector'dan
        /// elle ya da eski bir kayittan gelebilir. Hedef 0 olursa
        /// "0/0" gibi anlamsiz ekranlar cikar ve gorev ilk saniyede biter.
        /// </summary>
        public void Sanitize()
        {
            if (id == null) id = "";
            if (title == null) title = "";
            if (description == null) description = "";
            if (targetValue < 1) targetValue = 1;
            if (rewardMoney < 0) rewardMoney = 0;
            if (rewardXp < 0) rewardXp = 0;
        }

        // ------------------------------------------------------------------
        // Guvenli okuyucular: MissionSystem tanimlari DEGISTIRMEZ, bu
        // ozellikler sayesinde kirli veriyi de okurken onarilmis gorur.
        // ------------------------------------------------------------------

        public string SafeId => id == null ? "" : id;
        public string SafeTitle => title == null ? "" : title;
        public string SafeDescription => description == null ? "" : description;
        public int SafeTargetValue => targetValue < 1 ? 1 : targetValue;
        public int SafeRewardMoney => rewardMoney < 0 ? 0 : rewardMoney;
        public int SafeRewardXp => rewardXp < 0 ? 0 : rewardXp;

        /// <summary>Bu gorevin oncelik filtresi var mi? (sadece DeliverPriority)</summary>
        public bool FiltersPriority => kind == MissionKind.DeliverPriority;

        /// <summary>Turun varsayilan aciklamasi (elle yazilmadiysa).</summary>
        public static string DefaultDescription(MissionKind kind, int target,
                                               ParcelPriority priority)
        {
            switch (kind)
            {
                case MissionKind.DeliverCount:
                    return "Bugun " + target + " kargo teslim et.";
                case MissionKind.EarnMoney:
                    return "Bugun " + target + " TL kazanc yap.";
                case MissionKind.ReachLevel:
                    return "Bugun " + target + ". seviyeye ulas.";
                case MissionKind.DeliverPriority:
                    return "Bugun " + priority.DisplayName() +
                           " onceliginde " + target + " kargo teslim et.";
                case MissionKind.NoFailures:
                    return "Bugun hicbir teslimat basarisiz olmasin.";
                case MissionKind.OnTimeRate:
                    return "Teslimatlarin en az %" + target + "'i zamaninda olsun.";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Basliktan deterministik id uretir. Ayni tur + ayni baslik her
        /// zaman ayni id'yi verir; Save/UI tarafinda id ile konusulabilir.
        /// </summary>
        public static string BuildId(MissionKind kind, string title, int target)
        {
            string slug = Slug(title);
            if (slug.Length > 0) return kind + "_" + slug;
            return kind + "_" + target;
        }

        /// <summary>Basligi kucuk harfli, sadece [a-z0-9_] karakterli slug'a cevirir.</summary>
        private static string Slug(string source)
        {
            if (string.IsNullOrEmpty(source)) return "";

            var buffer = new char[source.Length];
            int length = 0;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c >= 'A' && c <= 'Z') c = (char)(c - 'A' + 'a');

                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                buffer[length] = ok ? c : '_';
                length++;
            }

            return new string(buffer, 0, length).Trim('_');
        }

        public override string ToString() =>
            SafeId + " [" + kind + " hedef " + SafeTargetValue +
            ", odul " + SafeRewardMoney + " TL / " + SafeRewardXp + " XP]";
    }
}
