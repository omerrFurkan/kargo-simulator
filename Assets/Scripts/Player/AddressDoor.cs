using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.NPC;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Adres kapısı: kargo tesliminin SON NOKTASI.
    ///
    /// AKIS (Asama 13):
    ///   E -> kapi calinir -> NPC cevap verir
    ///      -> NPC teslimat kodunu soyler -> oyuncu kodu girer
    ///      -> dogrudan girilirse <see cref="DeliverySystem.TryDeliver"/>
    ///         cagrilir ve MEVCUT ekonomi yolu calisir.
    ///
    /// EKONOMI NEDEN DEGISMEDI: para ve XP, <c>GameEvents.ParcelDelivered</c>
    /// olayindan gelir. Kod dogrulanmadan bu olay YAYINLANMAZ, yani
    /// yanlis kodda odeme olmaz. EkonomiService'in hicbir kodu
    /// ogrenmesi gerekmedi - guvenlik kapi teslimin ONUNDE kuruldu.
    ///
    /// AYIRMA KURALI (brief'in uyarisi): NPC durumu ile kargo durumu
    /// karismaz.
    ///   NPC  : NpcState (NotHome / HomeIdle / Answered / Closed) -
    ///          sadece kapidan konusma durumu.
    ///   Kargo: ParcelState (Ready / Loaded / Delivered ...) - sadece
    ///          kargonun yolculugu.
    /// Kod dogrulanmadan hicbir kargo durumu DEGISMEZ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AddressDoor : MonoBehaviour, IInteractable
    {
        [Tooltip("Kapinin onundeki etkilesim noktasi (yerel).")]
        [SerializeField] private Vector3 interactOffset = new Vector3(0f, 0f, 1.6f);

        private DeliverySystem _delivery;
        private CourierController _courier;
        private NPCSystem _npc;

        // --- Bekleyen teslimat oturumu ---
        private Parcel _pending;
        private int _pendingStopId = -1;

        /// <summary>Kod girisi bekleyen kargo (yoksa null).</summary>
        public Parcel PendingParcel => _pending;

        /// <summary>Bu kapida kod girisi acik mi?</summary>
        public bool IsAwaitingCode => _pending != null;

        public void Configure(DeliverySystem delivery, CourierController courier)
        {
            _delivery = delivery;
            _courier = courier;
        }

        /// <summary>NPC sistemini baglar (CompositionRoot cagirir).</summary>
        public void SetNpcSystem(NPCSystem npc) => _npc = npc;

        // ------------------------------------------------------------------
        // IInteractable
        // ------------------------------------------------------------------

        public string Prompt
        {
            get
            {
                if (_courier == null) return "";
                if (_delivery == null || _delivery.CurrentStop == null) return "";

                DeliveryStop stop = _delivery.CurrentStop;
                int pending = stop.PendingCount;
                if (pending <= 0) return "Adres tamamlandi";

                // Kod ekrani acikken baska bir ipucu gosterme.
                if (IsAwaitingCode) return "Teslimat kodunu gir";

                if (!_courier.IsCarrying) return "Kargo yok (elimde)";

                if (!CarriesForThisStop())
                    return "Bu adrese ait kargo yok (elde " + _courier.CarriedCount + ")";

                // Musteri evde degilse oyuncuyu bosuna kapida bekletme.
                if (_npc != null && _npc.IsHome(stop.DestinationId) == false)
                    return "Musteri evde degil";

                return "E: Kapiyi cal (" + pending + " bekliyor)";
            }
        }

        public bool CanInteract(CourierController courier)
        {
            if (courier == null || _delivery == null) return false;
            if (_delivery.CurrentStop == null) return false;
            if (_delivery.CurrentStop.PendingCount <= 0) return false;
            if (!courier.IsCarrying) return false;

            return CarriesForThisStop();
        }

        public void Interact(CourierController courier)
        {
            // Kod ekrani zaten acikken E tekrar calismasin.
            if (IsAwaitingCode) return;
            if (!CanInteract(courier)) return;

            DeliveryStop stop = _delivery.CurrentStop;
            if (stop == null) return;

            GameEvents.RaiseDoorKnocked(stop.DestinationId);

            // 1) Musteri evde degilse: teslim OLMAZ, kargo elde kalir.
            if (_npc != null && _npc.AnswerDoor(stop.DestinationId) == false)
            {
                GameEvents.RaiseCustomerNotHome(stop.DestinationId);
                courier.PlayAction("interact-right", 0.4f);
                return;
            }

            // 2) Bu adrese ait kargoyu sec.
            Parcel parcel = FindParcelForStop(courier, stop);
            if (parcel == null) return;

            // Konusma baslat: kod bekleniyor, HICBIR KARGO DURUMU
            //    degismez ve odeme YOK.
            _pending = parcel;
            _pendingStopId = stop.DestinationId;
            WrongAttempts = 0;

            int expected = parcel.DeliveryCode;
            string npcName = _npc != null && _npc.Get(stop.DestinationId) != null
                ? _npc.Get(stop.DestinationId).Name
                : "Musteri";

            GameEvents.RaiseNPCAnswered(stop.DestinationId, npcName, expected);
            courier.PlayAction("interact-right", 0.5f);
        }

        /// <summary>
        /// Oyuncu teslimat kodunu gonderir.
        ///
        /// DOGRULAMA BURADA: <see cref="DeliverySystem.TryDeliver"/> SADECE
        /// dogru koddan sonra cagrilir. Yanlista hicbir kargo durumu
        /// degismez, para verilmez.
        /// </summary>
        /// <returns>
        /// True  -> kod dogrulandi, teslimat tamamlandi.
        /// False -> kod yanlis (veya oturum kapali), kargo elde kaldi.
        /// </returns>
        public bool SubmitCode(string input)
        {
            if (_pending == null) return false;

            Parcel parcel = _pending;
            int expected = parcel.DeliveryCode;

            bool correct = Cargo.DeliveryCode.Verify(expected, input);

            // HER giris sayilir: basarili da basarisiz da.
            GameEvents.RaiseDeliveryCodeSubmitted(parcel, input, correct);

            if (correct == false)
            {
                WrongAttempts++;
                // Kargo ELDE KALIR ve oturum ACIK kalir: oyuncu ayni
                // kapida tekrar deneyebilir. Brief'in "kargo tekrar
                // teslim edilebilir" kurali bu; oturumu kapatmak
                // oyuncuyu her hata icin kapiya tekrar gitmeye zorlardi.
                //
                // Ceza HER YANLIS GIRIS icin uygulanir (olaydan). Tek
                // seferlik ceza istenirse dinleyici tarafta sayilir.
                GameEvents.RaiseDeliveryCodeWrong(parcel);
                return false;
            }

            GameEvents.RaiseDeliveryCodeCorrect(parcel);

            // Oturumu ONCE kapat: TryDeliver icinde ParcelDelivered
            // yayinlaniyor ve bir dinleyici bize geri donup soru
            // sorabilir; acik kalmis oturum olmamali.
            _pending = null;
            _pendingStopId = -1;

            // MEVCUT TESLIMAT YOLU. Buradan sonrasi degismedi: ekonomi,
            // seviye, rotalar, kayit tam olarak eskisi gibi calisir.
            DeliveryOutcome? outcome = _delivery.TryDeliver(parcel.Id);

            // Kargo basarili teslim EDILDIYSE elden dusulur. Basarisiz
            // teslimde (kirilgan kargo hasarli) kargo elde kalir ki
            // oyuncu tekrar deneyebilsin.
            if (outcome != null && outcome.Value != DeliveryOutcome.Failed)
                _courier?.DropOne(parcel);

            _npc?.CloseConversation(parcel.DestinationId);
            return outcome != null;
        }

        /// <summary>Bu oturumda yapilan yanlis giris sayisi.</summary>
        public int WrongAttempts { get; private set; }

        /// <summary>Oyuncu vazgecildi (Esc). Kargo elde kalir.</summary>
        public void CancelCodeEntry()
        {
            if (_pending == null) return;

            int destination = _pending.DestinationId;
            _pending = null;
            _pendingStopId = -1;
            _npc?.CloseConversation(destination);
        }

        private Parcel FindParcelForStop(CourierController courier, DeliveryStop stop)
        {
            var carried = courier.CarriedParcels;
            if (carried == null) return null;

            for (int i = 0; i < carried.Count; i++)
            {
                Parcel parcel = carried[i];
                if (parcel == null) continue;
                if (parcel.DestinationId != stop.DestinationId) continue;

                // TESLIMAT ONCESI KARGONUN PAKETLENMIS OLMASI GEREKIR.
                // requiresPackaging kapaliysa bu kontrol atlanir.
                if (_delivery != null && _delivery.Cargo != null &&
                    _delivery.Cargo.Manifest.RequiresPackaging &&
                    parcel.IsPackaged == false)
                {
                    continue;
                }

                return parcel;
            }

            return null;
        }

        /// <summary>Eldeki kutulardan bu adrese ait bir tane var mi?</summary>
        private bool CarriesForThisStop()
        {
            if (_delivery.CurrentStop == null) return false;

            var carried = _courier != null ? _courier.CarriedParcels : null;
            if (carried == null) return false;

            for (int i = 0; i < carried.Count; i++)
            {
                if (carried[i] == null) continue;
                if (carried[i].DestinationId == _delivery.CurrentStop.DestinationId) return true;
            }
            return false;
        }

          public Vector3 InteractWorldPosition()
          {
              return transform.TransformPoint(interactOffset);
          }

          // ===============================================================
          // TEST KANCALARI (kargoMekanigi.md #88)
          //
          // "Debug araclari olabilir ama release gameplay'de
          // kullanilmayacak. Test araclari Editor/Tests alaninda
          // tutulmali." Bu metodlar OYUNDA CAGIRILMAZ; yalnizca
          // otomatik testler icindir. Oyuncu yolu degismez.
          // ===============================================================

          /// <summary>
          /// Kapinin TUM teslim kuralini test icinden calistirir ve
          /// oyuncunun gorduguu sonucu dondurur.
          /// Donus: bos = basarili, dolu = ret sebebi.
          /// </summary>
          public string TryDeliverForTest(Parcel parcel)
          {
              if (parcel == null) return "Kargo yok.";

              // #53 / #100: YANLIS ADRES kontrolu. Teslim, NPC ve kod
              // ONCESI yapilir; NPC varsa bile yanlis adreste teslim
              // BASLAMAZ.
              int doorId = DoorAddressId();
              if (doorId >= 0 && parcel.DestinationId != doorId)
                  return "Bu kargo bu adrese ait degil.";

              if (parcel.State == ParcelState.Delivered)
                  return "Bu kargo zaten teslim edildi.";

              if (parcel.State != ParcelState.Carried)
                  return "Kargo elde degil.";

              return "";
          }

          /// <summary>Kapiyi cal: NPC evde mi? Oturum acildi mi?</summary>
          public bool KnockForTest()
          {
              if (_delivery == null || _delivery.CurrentStop == null) return false;
              return _npc == null || _npc.AnswerDoor(_delivery.CurrentStop.DestinationId);
          }

          /// <summary>Kod dene. Dogruysa teslim, yanlissa kargo ELDE kalir.</summary>
          public bool SubmitCodeForTest(int code)
          {
              if (_pending == null) return false;
              return SubmitCode(Cargo.DeliveryCode.Format(code));
          }

          /// <summary>Kapi nesnesinin adindan adres numarasini cozer.</summary>
          private int DoorAddressId()
          {
              Transform t = transform;
              for (int up = 0; t != null && up < 4; up++, t = t.parent)
              {
                  string n = t.name;
                  int k = n.LastIndexOf('_');
                  if (k < 0) continue;
                  if (int.TryParse(n.Substring(k + 1), out int v)) return v;
              }
              return -1;
          }
      }
  }

