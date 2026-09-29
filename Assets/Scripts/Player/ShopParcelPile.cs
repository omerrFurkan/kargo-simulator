using KargoSim.Cargo;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Dükkân rafı: depoda bekleyen kargoların durduğu yer.
    ///
    /// Oyuncu karakteri buraya gelip E'ye basar ve eline bir kutu alır.
    /// Kutu sayisi kurallari degil, GORSEL bir yigindir: rayda ne kadar
    /// kargo varsa o kadar kutu durur, alindikça bir tanesi eksilir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopParcelPile : MonoBehaviour, IInteractable
    {
        [Tooltip("Rayda gosterilecek en fazla kutu (gorsel).")]
        [SerializeField] private int visualSlots = 12;

        private CargoSystem _cargo;
        private Transform _visualRoot;
        private readonly System.Collections.Generic.List<GameObject> _boxes =
            new System.Collections.Generic.List<GameObject>();

        public void Configure(CargoSystem cargo, Transform visualRoot)
        {
            _cargo = cargo;
            _visualRoot = visualRoot;

            // Kargo degistikce rafin goruntusu de yenilemeli. Once sadece
            // Configure'de bir kez cagriliyordu: aracta 3 kargo varken
            // rafta 12 kutu duruyor, oyuncu araci bos saniyordu.
            Core.GameEvents.CargoChanged += OnCargoChanged;
            OnCargoChanged();
        }

        private void OnDestroy()
        {
            Core.GameEvents.CargoChanged -= OnCargoChanged;
        }

        public string Prompt
        {
            get
            {
                if (_cargo == null) return "";
                int waiting = _cargo.Manifest.AvailableCount;
                if (waiting <= 0) return "Rafta kargo yok";
                if (_full) return "Elden zaten dolu";
                return "E: Kargo al (" + waiting + " bekliyor)";
            }
        }

        /// <summary>
        /// Kurye ayakta degilse (arac icindeyken) rafta etkilesim kurulamaz.
        /// </summary>
        private bool _full;

        public bool CanInteract(CourierController courier)
        {
            _full = false;

            if (courier == null || _cargo == null) return false;
            if (_cargo.Manifest.AvailableCount <= 0) return false;

            if (!courier.CanCarryMore)
            {
                _full = true;
                return false;
            }

            return true;
        }

        public void Interact(CourierController courier)
        {
            if (!CanInteract(courier)) return;

            // ASAMA 15: TEK KUTU al.
            //
            // Once burasi "elde dolana kadar" aliyordu: oyuncu 3 kutuyla
            // cikip masaya gidiyor, paketleme 18 E basisi suruyor ve
            // "tek tek al" mumkun degildi. Brief bunu acikca istiyordu.
            //
            // Artik her E tek kutu alir; istiyorsa oyuncu E'ye tekrar
            // basar. Bu, oyuncunun yuruyerek kutuyu istedigi yere
            // goturmesini mumkun kilar.
            Parcel taken = null;
            var available = _cargo.Manifest.Available;

            for (int i = 0; i < available.Count; i++)
            {
                if (available[i] == null) continue;

                // ASAMA 20: ELDEKI KUTUYU ATLA.
                //
                // Kargo araca yuklenene kadar depoda "bekliyor" durumunda
                // kalir. Raf da eldeki kutu sistemde oldugu icin onu
                // tekrar tekrar veriyordu: oyuncu E'ye 3 kez basinca
                // "#1" kimlikli AYNI kutunun 3 kopyasi ele gecyordu ve
                // araca yalnizca 1'i sigiyordu ("3 kutu araca sigmadi").
                //
                // Raf, kuryenin zaten elinde tuttugu kutuyu vermemeli.
                if (courier.IsCarryingParcel(available[i].Id)) continue;

                if (!courier.PickUp(available[i])) break;

                _taken.Add(available[i].Id);
                taken = available[i];
                break;
            }

            if (taken != null)
            {
                courier.PlayAction("pick-up", 0.4f);
                Debug.Log("[ShopParcelPile] Kutu alindi: " + taken.Label
                          + " (elde " + courier.CarriedCount + ")");
            }

            Refresh();
        }

        /// <summary>
        /// Kuryenin ELINDE olup raftan kaldirilmis kargo kimlikleri.
        ///
        /// Neden gerekli: kargo, araca yuklenene kadar sistemde "depoda
        /// bekliyor" durumunda kalir (kaybolmamali). Ama gorsel olarak
        /// raftan alindiysa kutu orada durmamali; oyuncu "aldim ama
        /// rafta hala duruyor" der. Bu yuzden gorsel sayim sistem
        /// sayimindan ayrilir.
        /// </summary>
        private readonly System.Collections.Generic.HashSet<int> _taken = new System.Collections.Generic.HashSet<int>();

        /// <summary>Raftaki gorsel kutulari güncel kargo sayisina uydurur.</summary>
        public void Refresh()
        {
            if (_visualRoot == null) return;

            int total = _cargo != null ? _cargo.Manifest.AvailableCount : 0;
            if (total > visualSlots) total = visualSlots;

            // Elde tutulanlar raf sayisinden DUSULUR.
            int count = total - _taken.Count;
            if (count < 0) count = 0;

            while (_boxes.Count < count)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "ShelfBox";
                Destroy(go.GetComponent<Collider>());

                int index = _boxes.Count;
                // Raf üzerinde yan yana, hafif düzensiz dizilim: düz
                // bir sıra robot gibi görünmesin.
                go.transform.SetParent(_visualRoot, false);
                go.transform.localPosition = new Vector3(
                    (index % 4) * 0.46f - 0.7f,
                    0.9f + (index / 4) * 0.46f,
                    (index % 3) * 0.05f);
                go.transform.localRotation = Quaternion.Euler(0f, (index % 3) * 5f - 5f, 0f);
                go.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);

                _boxes.Add(go);
            }

            for (int i = 0; i < _boxes.Count; i++)
            {
                if (_boxes[i] != null) _boxes[i].SetActive(i < count);
            }
        }

        private void OnCargoChanged()
        {
            // Kargo araca yuklenince veya teslim edilince elden cikan
            // kayitlar temizlenir; aksi halde raf sayisi yanlis kalir.
            if (_cargo == null) return;

            _taken.RemoveWhere(id =>
            {
                Parcel p = _cargo.Manifest.FindById(id);
                return p == null || !p.IsActive || p.IsLoaded || p.IsDelivered;
            });

            Refresh();
        }

        /// <summary>
        /// Ozellik penceresi (asama 31) icin: rafta DURAN ve henuz
        /// alinmamis ilk kutu. Oyuncu fareyi rafa uzatip hangi kutunun
        /// kime gidecegini gorebilsin.
        /// </summary>
        public Cargo.Parcel HighlightParcel
        {
            get
            {
                var available = _cargo != null ? _cargo.Manifest.Available : null;
                if (available == null) return null;

                for (int i = 0; i < available.Count; i++)
                    if (available[i] != null) return available[i];

                return null;
            }
        }
    }
}
