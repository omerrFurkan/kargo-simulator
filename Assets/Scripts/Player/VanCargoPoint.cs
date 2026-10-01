using KargoSim.Cargo;
using KargoSim.Delivery;
using KargoSim.Vehicle;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Aracın kargo kapisi: iki isi vardir.
    ///
    ///  - Elde kargo varken  E  -> kargoyu araca koy
    ///  - Elde kargo yokken  E  -> araca bin
    ///
    /// Tek bir noktada iki davranis olmasi oyuncunun tek bir yer
    /// bulmasini saglar ("buraya gel, E'ye bas").
    ///
    /// SINIF ADI "Van" OLSA DA geneldir: fizik motoru yerine
    /// <see cref="IVehicleController"/> sozlesmesini kullanir, bu yuzden
    /// ayni nokta hem vani hem 3 tekerli kargo bisikletini yonetir
    /// (Asama 14). Sinif adi tarihsel bir izdir; yeniden adlandirmak
    /// tum cagrilari degistirirdi ve getirisi yoktu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VanCargoPoint : MonoBehaviour, IInteractable
    {
        [Tooltip("Aracin kapi konumu (yerel). Karakter burada belirir.")]
        [SerializeField] private Vector3 doorOffset = new Vector3(0f, 0.1f, -2.4f);

        private CargoSystem _cargo;
        private DeliverySystem _delivery;
        private CourierController _courier;
        private IVehicleController _vehicle;
        private Camera _vehicleCamera;

        public void Configure(CargoSystem cargo, DeliverySystem delivery, CourierController courier,
                              IVehicleController vehicle, Camera vehicleCamera)
        {
            _cargo = cargo;
            _delivery = delivery;
            _courier = courier;
            _vehicle = vehicle;
            _vehicleCamera = vehicleCamera;
        }

        /// <summary>Aracin GERCEK kargo alani (#30). Fiziksel slotlar, kapasite.</summary>
        [SerializeField] private VehicleCargoArea _area;

        public VehicleCargoArea Area
        {
            get
            {
                if (_area == null) _area = GetComponentInChildren<VehicleCargoArea>();
                return _area;
            }
            set { _area = value; }
        }

        public string Prompt
        {
            get
            {
                if (_courier == null || _courier.IsInVehicle) return "";

                string action;
                if (_courier.IsCarrying)
                    action = "E: Araca koy (" + _courier.CarriedCount + " kutu)";
                else if (_cargo != null && _cargo.Manifest.LoadedCount > 0)
                {
                    // Hangi adrese ait oldugunu SOYLE: oyuncu kutuyu
                    // alip kapida "bu adrese ait kargo yok" demesini
                    // yasamamali.
                    string where = HasCargoForCurrentStop() ? "bu adres icin" : "aracdan";
                    action = "E: Kargo al (" + _cargo.Manifest.LoadedCount + " aracda, " + where + ")";
                }
                else
                    action = "";

                // Araca binmek AYRI tus: E kargoyla ilgilenir, F biner.
                // Tek tusa sigdirilirsa arac doluysa oyuncu hic binemez.
                return action == "" ? "[F] Araca bin" : action + "    |    [F] Araca bin";
            }
        }

        public bool CanInteract(CourierController courier)
        {
            return courier != null && !courier.IsInVehicle;
        }

        public void Interact(CourierController courier)
        {
            if (!CanInteract(courier)) return;

            // YENI OYUNCU YOLU (kargoMekanigi.md #33, #41, #44, #45, #47).
            //
            // Onceki sistem: `E` kuryede kargo varsa hepsini yaziyor,
            // yoksa aractaki kargolardan BIRINI aliyordu. Simdi:
            //   Elde kargo var  -> TEK kargo araca konur (#122 toplu YASAK)
            //   Elde kargo yok  -> BAKILAN kutu kasadan alinir (#45)
            // Kapasite/temiz yuzey degilse kutuyu ELDE TUTARIZ (#41).
            var carry = courier != null ? courier.Carry : null;
            if (carry != null)
            {
                if (carry.HasCargo)
                {
                    if (_area != null)
                        carry.TryLoadToVehicle(_area);
                    return;
                }

                // Oyuncu hangi kutuya bakiyorsa O alinir.
                ParcelWorldObject looked = LookedAtBox(courier);
                if (looked != null && _area != null)
                {
                    carry.TryUnloadFromVehicle(_area, looked);
                    return;
                }
            }

            // Geriye donus uyumu: yeni tasiyici yoksa eski yol.
            if (courier.IsCarrying)
            {
                LoadCarried(courier);
                return;
            }

            if (_cargo != null && _cargo.Manifest.LoadedCount > 0 && courier.CanCarryMore)
            {
                TakeFromVehicle(courier);
                return;
            }

            Board(courier);
        }

        /// <summary>
        /// Oyuncunun BAKTIGI kutu (#45).
        ///
        /// "Rastgele bir kutu" secmek oyuncuyu yanlis kapinin onune
        /// goturuyordu; sistem baska kutuyu secebilmemeli.
        /// </summary>
        private ParcelWorldObject LookedAtBox(CourierController courier)
        {
            if (_area == null || courier == null) return null;

            Vector3 o = courier.transform.position + Vector3.up * 1.3f;
            RaycastHit[] hits = Physics.RaycastAll(
                o, courier.transform.forward, 4.2f, ~0, QueryTriggerInteraction.Collide);

            ParcelWorldObject best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var box = hits[i].collider.GetComponentInParent<ParcelWorldObject>();
                if (box == null) continue;
                if (box.Parcel == null) continue;
                if (box.Parcel.State != ParcelState.LoadedOnVehicle) continue;
                if (hits[i].distance < bestD) { bestD = hits[i].distance; best = box; }
            }
            return best;
        }

        /// <summary>
        /// Araktaki kargolardan birini kuryenin eline alir.
        ///
        /// Neden gerekli: Adres teslimi kuryenin eliyle yapiliyor;
        /// ama arac→el yolu HIC YOKTU. Oyuncu adrese gidip araci
        /// birakip eve yuruyor, kapida "kargoyu birak" dediginde eli
        /// BOS oluyor ve oyun hic para kazanamiyordu.
        ///
        /// ONCE BULUNAN ADRESIN KARGOSU ALINIR. Rastgele bir kutu
        /// secmek oyuncuyu yanlis kapinin onune gonderiyordu; kapi
        /// "bu adrese ait kargo yok" deyip etkilesimi reddediyor ve
        /// oyuncu ne yapacagini bilemiyordu. Adres biliniyorsa dogru
        /// kutu secilir; bilinmiyorsa (depoda) herhangi biri.
        ///
        /// Kargo sistemde "yuklendi" olarak kalir; gorsel olarak
        /// kuryeye gecer. Teslim edilene kadar listede durur, boylece
        /// kargo KAYBOLMAZ.
        /// </summary>
        private void TakeFromVehicle(CourierController courier)
        {
            if (_cargo == null) return;

            int wanted = WantedDestinationId();

            // 1) Oncelik: bu adrese ait kargo.
            for (int i = 0; i < _cargo.Manifest.Loaded.Count; i++)
            {
                Parcel parcel = _cargo.Manifest.Loaded[i];
                if (parcel == null) continue;
                if (wanted >= 0 && parcel.DestinationId != wanted) continue;

                // ASAMA 33: kutu listeden cikarilir ve "kuryede"
                // durumuna gecer; rota DeliverySystem tarafindan
                // araCta + ELDE birlikte kurulmaya devam eder.
                if (!courier.CanCarryMore) return;
                if (!_cargo.Manifest.UnloadParcel(parcel)) continue;
                if (!courier.PickUp(parcel))
                {
                    _cargo.Manifest.TryLoad(parcel, _delivery != null ? _delivery.Elapsed : 0f);
                    return;
                }
                courier.PlayAction("pick-up", 0.4f);
                Debug.Log("[VanCargoPoint] Kargo eline alindi: " + parcel.Label);
                return;
            }

            // 2) Bu adrese ait kargo yoksa herhangi biri (depo iadesi).
            for (int i = 0; i < _cargo.Manifest.Loaded.Count; i++)
            {
                Parcel parcel = _cargo.Manifest.Loaded[i];
                if (parcel == null) continue;

                // ASAMA 33: kutu listeden cikarilir ve "kuryede"
                // durumuna gecer; rota DeliverySystem tarafindan
                // araCta + ELDE birlikte kurulmaya devam eder.
                if (!courier.CanCarryMore) return;
                if (!_cargo.Manifest.UnloadParcel(parcel)) continue;
                if (!courier.PickUp(parcel))
                {
                    _cargo.Manifest.TryLoad(parcel, _delivery != null ? _delivery.Elapsed : 0f);
                    return;
                }
                courier.PlayAction("pick-up", 0.4f);
                Debug.Log("[VanCargoPoint] Kargo eline alindi: " + parcel.Label);
                return;
            }
        }

        /// <summary>Su anki duragin adres kimligi; yoksa -1.</summary>
        private int WantedDestinationId()
        {
            if (_delivery == null) return -1;
            if (_delivery.CurrentStop == null) return -1;
            return _delivery.CurrentStop.DestinationId;
        }

        /// <summary>Aracda bu adrese ait kargo var mi?</summary>
        private bool HasCargoForCurrentStop()
        {
            int wanted = WantedDestinationId();
            if (wanted < 0) return false;

            for (int i = 0; i < _cargo.Manifest.Loaded.Count; i++)
            {
                if (_cargo.Manifest.Loaded[i] != null &&
                    _cargo.Manifest.Loaded[i].DestinationId == wanted) return true;
            }
            return false;
        }

        /// <summary>
        /// Karakteri araca bindirir. UIManager'in F tusu ve testler
        /// AYNI metodu cagirir; boylece "F ile binme" yolu tek
        /// yerde yazilidir.
        /// </summary>
        public void Board(CourierController courier)
        {
            if (courier == null || courier.IsInVehicle) return;

            // ASAMA 18: BINME KONUMUNU HATIRLA.
            //
            // Oyuncu: "bisikletten inince ilk bindigim konumda oluyorum".
            // Once ExitSpot() sabit bir noktaydi (aracin 2.2 m yaninda),
            // o yuzden oyuncu bittigi yerde degil, her zaman aracin
            // hizasinda beliriyordu - ozellikle uzaktan binip inince
            // "beni nereye birakmissin" durumu olusuyordu.
            //
            // Artik oyuncunun GERCEKTEN bindigi konuma donulur.
            _lastBoardPosition = courier.transform.position;
            _hasBoardPosition = true;

            // Once BURAYA ExitVehicle cagriliyordu ve karakter arac
            // disinda kaldiigi icin oyuncu araca bir daha HICBIR ZAMAN
            // binemiyordu; oyun 2. adimda bitiyordu. Dogru cagri
            // EnterVehicle.
            //
            // Asama 14: binmeden ONCE kuryeye bu araci "aktif" et. Iki
            // arac oldugundan kurye hangi kamerayi ve hangi fizigi
            // kullanacagini bilmiyor.
            if (_vehicle != null) courier.SetActiveVehicle(_vehicle, _vehicleCamera);

            courier.EnterVehicle();
        }

        private Vector3 _lastBoardPosition;
        private bool _hasBoardPosition;

        /// <summary>
        /// Karakterin arac lastiginin yanina inmesi icin guvenli nokta.
        ///
        /// Once UIManager konyumu kendisi hesapliyordu ve hiz kontrolu
        /// yoktu: 90 km/h ile E'ye basinca karakter hareketli aracin
        /// altina firlatiliyordu.
        /// </summary>
        public Vector3 ExitSpot()
        {
            // ASAMA 20: INME YERI.
            //
            // Oyuncu: "bindigim yerde iniyorum, bisiklet nerde ben nerde".
            //
            // Bir onceki duzeltmede inme yeri DAIMA binme konumuna
            // baglandi. Bu, araci bir yere surup inince oyuncuyu yuzlerce
            // metre geriye isikliyordu: arac orada degil, oyuncu kilometre
            // uzakta kalirdi. Ozellikle kotu bir cozumdu.
            //
            // DOGRU KURAL:
            //   - Aracin YANINDASAN (yaklasik 5 m icinde) ayni yere in.
            //     Oyuncu hic gitmediyse yerinden oturmasin.
            //   - Arac uzaklastiysa aracin YANINA in. Kargo oyununda
            //     aracin gitmedigi yerde kurye olmamalidir.
            Transform reference = _vehicle != null ? _vehicle.VehicleTransform : transform;

            if (_hasBoardPosition)
            {
                float d = Vector3.Distance(_lastBoardPosition, reference.position);
                if (d <= NearVehicleDistance) return _lastBoardPosition;
            }

            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            return reference.position + right * 2.2f + Vector3.up * 0.1f;
        }

        /// <summary>Bu mesafeden yakinda kurye aracin yaninda sayilir.</summary>
        private const float NearVehicleDistance = 5f;

        /// <summary>Arac yavas mi? (Hizli arac birakilmaz.)</summary>
        public bool IsVehicleSlow()
        {
            return _vehicle == null || _vehicle.SpeedKmh < 6f;
        }

        /// <summary>
        /// Oyuncu aractan INDYKten sonra araci "yatistirir".
        ///
        /// ASAMA 15 (P0): oyuncunun CharacterController'i aracin BoxCollider'i
        /// icine girince fizik motoru araci itiyordu. Oyuncu biraki yerinden
        /// oynayinca arac da onunla birlikte kayiyor, yatip zigzag
        /// yapiyordu. Oyuncu ve arac birbirinden BAGIMSIZ olmali.
        ///
        /// Cozum: cikis aninda aracin hizini sifirla ve sonraki kareler
        /// icin surtunmeyi artir. Bicak kesmez, motoru durdurur; arac
        /// yine fiziksel olarak durur, yalnizca oyuncu onu itmez.
        /// </summary>
        public void SettleAfterExit()
        {
            var rb = _vehicle != null && _vehicle.VehicleTransform != null
                ? _vehicle.VehicleTransform.GetComponent<Rigidbody>()
                : null;

            if (rb == null) return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.angularDrag = 12f;
        }

        private void LoadCarried(CourierController courier)
        {
            if (_cargo == null) return;

            int moved = 0;
            int rejected = 0;

            // ELDEKI KARGONUN KENDISI yüklenir. Once burasi depodaki
            // "ilk kargo"yu arıyordu; oyuncu elinde #7,#8,#9 gorurken
            // araca #1,#2,#3 kaliyordu ve oyuncu hangi kutuyu
            // nereden aldigini takip edemiyordu.
            var carried = new System.Collections.Generic.List<Parcel>(courier.CarriedParcels);

            for (int i = 0; i < carried.Count; i++)
            {
                Parcel parcel = carried[i];
                if (parcel == null) continue;

                if (_cargo.TryLoadParcel(parcel.Id, _delivery != null ? _delivery.Elapsed : 0f))
                {
                    courier.DropOne(parcel);
                    moved++;
                }
                else
                {
                    // Kapasite doldu: bu kutu ELDE kalir, oyuncu
                    // tekrar deneyebilir.
                    rejected++;
                }
            }

            courier.PlayAction("pick-up", 0.4f);

              if (rejected > 0)
                  Debug.LogWarning("[VanCargoPoint] " + rejected + " kutu araca sigmadi (kapasite); elde kaldi.");
              if (moved > 0)
                  Debug.Log("[VanCargoPoint] " + moved + " kutu araca yuklendi.");

              // ASAMA 18: oyuncuya ACIKLA sebebini goster.
              //
              // Sessiz red oyuncuyu en cok yoran sey: kutu tasiyor,
              // aracin yaninda, E'ye basiyor, hicbir sey olmuyor ve
              // neden oldugunu oyuncu bilmiyor.
              if (moved == 0 && rejected > 0)
              {
                  bool anyUnpackaged = false;
                  for (int i = 0; i < carried.Count; i++)
                      if (carried[i] != null && !carried[i].IsPackaged) { anyUnpackaged = true; break; }

                  courier.LastActionHint = anyUnpackaged
                      ? "Bu kutu HENUZ PAKETLENMIS DEGIL. Once paketleme "
                        + "masasina gotur, 6 adimi tamamla, sonra araca koy."
                      : "Arac dolu. Kapasite: " + _cargo.Manifest.LoadedCount
                        + " kutu yuklu (kg/litre sinirinda)";
              }
              else
              {
                  courier.LastActionHint = moved > 0
                      ? moved + " kutu araca yuklendi."
                      : string.Empty;
              }
          }


        /// <summary>
        /// Ozellik penceresi (asama 31) icin gosterilecek kutu.
        ///
        /// Oncelik sirasi: kuryenin ELINDEKI kutu (yukleyecegi) yoksa
        /// aracdaki ilk kutu (alacagi). Boylece oyuncu hangi kutunun
        /// nereye gidecegini aracin yaninda gorur.
        /// </summary>
        public Cargo.Parcel HighlightParcel
        {
            get
            {
                if (_courier != null && _courier.IsCarrying && _courier.CarriedCount > 0)
                    return _courier.CarriedParcels[_courier.CarriedCount - 1];

                var loaded = _cargo != null ? _cargo.Manifest.Loaded : null;
                if (loaded == null) return null;

                for (int i = 0; i < loaded.Count; i++)
                    if (loaded[i] != null) return loaded[i];

                return null;
            }
        }
    }
}
