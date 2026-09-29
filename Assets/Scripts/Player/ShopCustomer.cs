using KargoSim.Cargo;
using KargoSim.Core;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// DUKKANA GELEN MUSTERI. Kargoyu oyuncunun eline verir.
    ///
    /// ASAMA 30 - YENI KUTU AKISI (oyuncunun secimi: B).
    ///
    /// Oyuncu istedi:
    ///   "npc ler gelsin, gelen npc ile konusup kutuyu alalim,
    ///    dukkana istedigimiz bir yere yerlestirelim, dagitima cikarken
    ///    elimize alip arabaya yerlestirelim"
    ///
    /// ESKI AKIIS: raf -> paketleme masasi (6 adim) -> araca yukle
    /// YENI  AKIIS: musteri -> kutu (PAKETLI gelir) -> istedigin yere
    ///               koy -> eline al -> araya koy
    ///
    /// ONEMLI: kutu burada TAMAMEN PAKETLIDIR. `IsPackaged` yalnizca
    /// `PackagingSteps.IsReady(...)` oldugu icin dogru; oyuncu masada
    /// alti adim beklemez. Paketleme sistemi SILINMEDI - baska akislar
    /// ve testler kullanmaya devam ediyor, sadece zorunlu adim olmaktan
    /// cikti.
    ///
    /// Sahne degisikligi YAPILMAZ: musteriler CompositionRoot tarafindan
    /// kaldirilip kurulur. Elle sahne duzenlemek defalarca sorun
    /// yaratti; uretim kodu tek dogruluk kaynagi.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopCustomer : MonoBehaviour, IInteractable
    {
        [Tooltip("Kuryenin bu musteriye bakacagi nokta (kafasi).")]
        [SerializeField] private float talkHeight = 1.6f;

        [Tooltip("Musterinin adi; HUD ipucunda gorunur.")]
        [SerializeField] private string displayName = "Musteri";

        private CargoSystem _cargo;
        private CourierController _courier;

        /// <summary>Bu musteriden alinan kutu sayisi (test icin).</summary>
        public int HandedOverCount { get; private set; }

        public string DisplayName => displayName;

        public Vector3 InteractWorldPosition() => transform.position + Vector3.up * talkHeight;

        public string Prompt
        {
            get
            {
                if (_courier == null) return "";
                if (_courier.IsInVehicle) return "";
                if (HandedOverCount > 0) return displayName + ": teslim ettin, gorusuruz";
                if (!HasParcelToGive()) return displayName + ": yaninda kargo yok";
                if (_courier.IsCarrying) return "Elden bosalmadan kutu alinamaz";
                return "E: " + displayName + " ile konus, kutusunu al";
            }
        }

        public bool CanInteract(CourierController courier)
        {
            if (courier == null || courier.IsInVehicle) return false;
            if (HandedOverCount > 0) return false;
            if (!HasParcelToGive()) return false;

            // Eller doluyken kutu alinamaz; oyuncu once elindekini
            // birakmalidir. Bu, kutu sayisini yonetilebilir kilar.
            if (courier.IsCarrying) return false;

            return courier.CanCarryMore;
        }

        public void Interact(CourierController courier)
        {
            if (!CanInteract(courier)) return;

            var available = _cargo != null ? _cargo.Manifest.Available : null;
            if (available == null || available.Count == 0) return;

            for (int i = 0; i < available.Count; i++)
            {
                Parcel parcel = available[i];
                if (parcel == null) continue;

                // Kutu musteri tarafindan PAKETLENMIS gelir.
                //
                // `IsPackaged` yalnizca PackagingSteps.IsReady(...) oldugu
                // icin, adim ilerleterek hazir hale getiriyoruz. Boylece
                // oyuncu masada alti adim beklemez - istedigi akis bu.
                int guard = 0;
                while (!parcel.IsPackaged && guard++ < 32)
                {
                    if (!parcel.AdvancePackaging()) break;
                }

                if (!courier.PickUp(parcel)) return;

                HandedOverCount++;
                courier.PlayAction("interact-left", 0.4f);
                GameEvents.RaiseCargoChanged();
                return;
            }
        }

        /// <summary>Bu musteri verebilecegi kutu var mi?</summary>
        public bool HasParcelToGive()
        {
            var available = _cargo != null ? _cargo.Manifest.Available : null;
            if (available == null) return false;
            for (int i = 0; i < available.Count; i++)
                if (available[i] != null) return true;
            return false;
        }

        public void Configure(CargoSystem cargo, CourierController courier)
        {
            _cargo = cargo;
            _courier = courier;
        }

        /// <summary>Editor kancasi: musteri adi.</summary>
        public void ConfigureForSetup(string name) => displayName = name;
    }
}
