using System.Collections.Generic;
using System.Text;
using KargoSim.Cargo;
using KargoSim.Player;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// KARAKTER ENVANTERI + ARAC BAGAJI (gorev #26, #89, #90).
    ///
    /// Oyuncu istedi:
    ///   "oyuncu envanterini her zaman acabilir, araclarin kargo
    ///    yukledigimiz bagajlari acilir olsun, kendi envanterimizden
    ///    aracin bagajindan kutu alip yerlestirebilelim,
    ///    gittigimiz yere gore kargolarimizi gorelim"
    ///
    /// ACILMA: I tusu (TAB depoya ozgu kalir).
    ///
    /// SLOT ISLEMLERI:
    ///   [1-3]  eldeki kutu -> araca koy
    ///   [4-6]  aractaki kutu -> ele al
    ///   [ESC]  kapat
    ///
    /// NEDEN NUMARA TUSU (fare degil): proje zaten klavyeyle oynanÄ±yor
    /// ve depo paneli 1-9 ile calisiyor. Ayni gelenek, ayri ogrenme maliyeti.
    ///
    /// SIRALAMA: kutular HEDEFE GORE dizilir (once kucuk adres, ayni
    /// adres icinde kutu numarasi). Oyuncu "gittigimiz yere gore
    /// kargolarimizi gorelim" dedi; teslim sirasinda hangi kutuyu once
    /// cikarmak gerektigi gorunsun.
    ///
    /// YAZI URETIMI saf (statik) fonksiyonlarda: "ne yaziyor" sorusu
    /// ekran goruntulenmeden test edilebilir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CargoInventoryView : MonoBehaviour
    {
        [Header("Metinler")]
        [SerializeField] private Text handText;
        [SerializeField] private Text vehicleText;
        [SerializeField] private Text capacityText;
        [SerializeField] private Text hintText;

        [Header("Gorunurluk")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;

        private CargoSystem _cargo;
        private CourierController _courier;
        private Delivery.DeliverySystem _delivery;

        /// <summary>Eldeki kutu sayisi icin gosterilen azami slot.</summary>
        public const int MaxSlots = 3;

        /// <summary>Arac slotlari bu numaradan baslar (tus cakismasin).</summary>
        public const int VehicleSlotsStart = 4;

        public bool IsOpen { get; private set; }

        /// <summary>Son islemin sonucu (HUD ipucunda gosterilir).</summary>
        public string LastMessage { get; private set; } = "";

        public void Configure(CargoSystem cargo, CourierController courier,
                              Delivery.DeliverySystem delivery)
        {
            _cargo = cargo;
            _courier = courier;
            _delivery = delivery;
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            ApplyVisibility();
            if (IsOpen) Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (group != null) group.alpha = IsOpen ? 1f : 0f;
            if (group != null) group.blocksRaycasts = IsOpen;
            if (panel != null) panel.gameObject.SetActive(IsOpen);
        }

        // ==================================================================
        // TRANSFER YOK - BILINCLI KARAR
        //
        // Onceki surumde bu panel [1-3] / [4-6] tuslariyla kargoyu
        // araca/ele "aktariyordu". Bu kargoMekanigi.md #23, #94 ve #120'nin
        // ACIK YASAGI:
        //   "TAB paneli fiziksel kargoyu teleportun kisayolu OLMAMALI"
        //   "Panelden secmek oyuncunun onu eline teleport ETMEMELI"
        //   "E -> +1 Cargo gibi calismayacak"
        //
        // Bu yuzden transfer kodu TAMAMEN SILINDI. Panel artik yalnizca
        // BILGI gosterir. Kargoyu almak / birakmak / yuklemek icin oyuncu
        // fiziksel olarak E ve G kullanir.
        // ==================================================================

        // ==================================================================
        // YAZI (saf, test edilebilir)
        // ==================================================================

        /// <summary>Kutulari hedefe gore siralar: once kucuk adres, ayni adres icinde kutu no.</summary>
        public static List<Parcel> SortedByDestination(IReadOnlyList<Parcel> source)
        {
            var result = new List<Parcel>();
            if (source == null) return result;

            for (int i = 0; i < source.Count; i++)
                if (source[i] != null) result.Add(source[i]);

            result.Sort((a, b) =>
            {
                int d = a.DestinationId.CompareTo(b.DestinationId);
                return d != 0 ? d : a.Id.CompareTo(b.Id);
            });
            return result;
        }

        /// <summary>"ELDE" listesi: "[1] #7 Derya Akin - adres 3 (2.9 kg)".</summary>
        public static string FormatHand(IReadOnlyList<Parcel> source)
        {
            var sorted = SortedByDestination(source);
            var sb = new StringBuilder();

            for (int i = 0; i < sorted.Count; i++)
            {
                Parcel p = sorted[i];
                sb.AppendLine("[" + (i + 1) + "] #" + p.Id + "  " + p.RecipientName
                              + "  adres " + p.DestinationId
                              + "  (" + p.WeightKg.ToString("0.0") + " kg)");
            }
            if (sorted.Count == 0) sb.AppendLine("(bos)");
            return sb.ToString().TrimEnd();
        }

        /// <summary>"ARACTA" listesi: "[4] #7 ... (2.9 kg / 8.9 L)".</summary>
        public static string FormatVehicle(IReadOnlyList<Parcel> source)
        {
            var sorted = SortedByDestination(source);
            var sb = new StringBuilder();

            for (int i = 0; i < sorted.Count; i++)
            {
                Parcel p = sorted[i];
                sb.AppendLine("[" + (VehicleSlotsStart + i) + "] #" + p.Id + "  "
                              + p.RecipientName + "  adres " + p.DestinationId
                              + "  (" + p.WeightKg.ToString("0.0") + " kg / "
                              + p.VolumeLiters.ToString("0.0") + " L)");
            }
            if (sorted.Count == 0) sb.AppendLine("(bos)");
            return sb.ToString().TrimEnd();
        }

        /// <summary>"Yuk: 3.5 / 60.0 kg    11.3 / 150.0 L".</summary>
        public static string FormatCapacity(CargoManifest manifest)
        {
            if (manifest == null) return "";

            return "Yuk: " + manifest.LoadedWeightKg.ToString("0.0")
                 + " / " + manifest.Capacity.MaxWeightKg.ToString("0.0") + " kg"
                 + "     " + manifest.LoadedVolumeLiters.ToString("0.0")
                 + " / " + manifest.Capacity.MaxVolumeLiters.ToString("0.0") + " L";
        }

        public void Refresh()
        {
            if (_cargo == null || !IsOpen) return;

            if (handText != null) handText.text = "ELDE\n" + FormatHand(_courier.CarriedParcels);
            if (vehicleText != null) vehicleText.text = "ARACTA\n" + FormatVehicle(_cargo.Manifest.Loaded);
            if (capacityText != null) capacityText.text = FormatCapacity(_cargo.Manifest);

            if (hintText != null)
            {
                hintText.text = "[1-3] Eldeki kutuyu araca    [4-6] Aracdan eline al"
                              + "    [I/Esc] Kapat"
                              + (string.IsNullOrEmpty(LastMessage) ? "" : "\n" + LastMessage);
            }
        }
    }
}
