using System.Collections.Generic;
using UnityEngine;

namespace KargoSim.Cargo
{
    /// <summary>
    /// PLAYER TEK-KARGO TASIYICI (kargoMekaniÃ„Å¸i.md #10, #11, #12, #13,
    /// #14, #15, #16, #17, #18, #66, #119, #122, #123).
    ///
    /// EN KRITIK KURAL: ayni anda SADECE 1 fiziksel kargo.
    ///
    /// ONCEKI SISTEMDE `maxCarry = 3` idi ve kurye 3 kutu YIGIN
    /// halde tasiyordu. Bu yuzden:
    ///   - oyuncu kutuyu "tasiyor" hissi kayboluyordu
    ///   - kargo gizlice envantere donusuyordu (#120 yasak)
    ///   - toplu yukleme/cikarma dogal gorunuyordu (#122/#123 yasak)
    /// Artik kapasite 1. Gerekirse 3 kutu icin 3 kez tasima yapilir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CargoCarryController : MonoBehaviour
    {
        [Header("Tuslar")]
        [SerializeField] private KeyCode dropKey = KeyCode.G;

        [Header("Elden tasima noktasi")]
        [Tooltip("Kamera altindaki nokta. Kutu gorunur ama yuzu/kamerayi kapatmaz (#12, #71).")]
        [SerializeField] private Transform holdAnchor;

        [Header("Hiz")]
        [SerializeField] private float holdSpeedMultiplier = 0.8f;
        [SerializeField] private float holdRunMultiplier = 0.65f;

        private ParcelWorldObject _held;



        /// <summary>

        /// Manifest ADAPTER (kargoMekanigi.md #3).

        ///

        /// Yeni tasiyici kargoyu fiziksel olarak tasir; ama teslimat

        /// sistemi kargonun ARAcTA veya ELDE oldugunu MANIFESTten

        /// ogreniyor. Bu olmadan rota kuryedeki kutuyu GORMUYORDU

        /// ve teslim mumkun olmuyordu.

        /// 

        /// Kargo hicbir zaman kaybolmaz; sadece listeler guncellenir.

        /// </summary>

        private CargoManifest _manifest;



        public void SetManifest(CargoManifest m) => _manifest = m;

        /// <summary>Oyuncunun elinde fiziksel kutu var mi?</summary>
        public bool HasCargo => _held != null;

        /// <summary>Eldeki kutu nesnesi (yoksa null).</summary>
        public ParcelWorldObject HeldBox => _held;

        /// <summary>Eldeki kargonun DATASI (yoksa null).</summary>
        public Parcel HeldParcel => _held != null ? _held.Parcel : null;

        public Transform HoldAnchor => holdAnchor;
        public KeyCode DropKey => dropKey;

        /// <summary>Son islem sonucu - HUD ipucunda gosterilir (#17).</summary>
        public string LastMessage { get; private set; } = "";

        public void ConfigureAnchor(Transform anchor) => holdAnchor = anchor;

        // ===============================================================
        // ALMA - fiziksel nesne, otomatik envanter DEGIL (#120)
        // ===============================================================
        /// <summary>
        /// Dunya yuzeyindeki kutuyu ele al.
        ///
        /// BASARISIZ OLMASI GEREKEN DURUMLAR:
        ///   - zaten bir kargo eldeyse  (N2)
        ///   - kutu 5 metreden uzaksa   (N1)
        ///   - kargo teslim edilmisse  (N8)
        ///   - kutu arac kazada ve oyuncu bakinca degilse
        /// Gecis basarisiz oldugunda kutu ELDE KALIR; hicbir kosulda
        /// kaybolmaz (#57).
        /// </summary>
        public bool TryPickup(ParcelWorldObject box, Collider playerCollider)
        {
            LastMessage = "";

            if (box == null || box.Parcel == null)
            {
                LastMessage = "Burada kargo yok.";
                return false;
            }

            if (HasCargo)
            {
                LastMessage = "Once elindeki kargoyu birak.";   // #21
                return false;
            }

            if (box.Parcel.State == ParcelState.Delivered)
            {
                LastMessage = "Bu kargo zaten teslim edildi.";   // N8
                return false;
            }

            if (box.Parcel.State == ParcelState.Cancelled)
            {
                LastMessage = "Bu kargo iptal edildi.";
                return false;
            }

            if (!ParcelStateRules.CanGoTo(box.Parcel.State, ParcelState.Carried))
            {
                LastMessage = "Bu kargo su an alinamaz.";
                return false;
            }

            // Fiziksel gecis: world -> held
            box.Parcel.TryChangeState(ParcelState.Carried);
            box.transform.SetParent(holdAnchor, false);
            box.transform.localPosition = Vector3.zero;
            box.transform.localRotation = Quaternion.identity;
            box.AttachIgnore(playerCollider, null);
            box.ApplyPhysicsForState(ParcelState.Carried);

            if (_manifest != null) _manifest.NoteCarriedByPlayer(box.Parcel);

            _held = box;
            LastMessage = "Kargo elinde: " + box.Parcel.ShortLabel;
            return true;
        }

        // ===============================================================
        // BIRAKMA - kontrollu, FIRLATMA DEGIL (#15, #18)
        // ===============================================================
        /// <summary>
        /// Eldeki kutuyu oyuncunun onundeki gecerli yuzeye koyar.
        ///
        /// G TUSU FIRLATMA TUSU DEGILDIR: fizik kuvveti uygulanmaz,
        /// kutu ileriye atilmaz. Gecersiz yuzeyde kutuyu ELDE TUTAR
        /// ve sebebini yazar (#17).
        /// </summary>
        public bool TryDrop(float maxForward = 1.9f, float maxDown = 3.2f)
        {
            LastMessage = "";
            if (!HasCargo)
            {
                LastMessage = "Elinde kargo yok.";
                return false;
            }

            Vector3 half = _held.BoxSize * 0.5f;

            // ---------------------------------------------------------------
            // BIRAKMA NOKTASI (kargoMekanigi.md #16)
            //
            // ONCEKI HATA: yalnizca yatay "forward raycast" atiliyordu.
            // Depo/kapi gibi acik alanda yatay isin HICBIR SEYE
            // carpamiyor ve sistem her seferinde "Buraya kargo
            // birakilamaz" diyordu - yani oyuncu kargoyu hicbir yere
            // birakamÃ„Â±yordu.
            //
            // Dogru cozum: (1) onde bir yuzey varsa onu kullan,
            //              (2) yoksa onun ALTINDAKI zemini bul.
            //              "Gogusten dogrudan yere dusurme" yasak.
            // ---------------------------------------------------------------
            Vector3 chest = transform.position + Vector3.up * 1.15f;
            Vector3 ahead = transform.position + transform.forward * 0.9f;

            Vector3 point = Vector3.zero;
            Vector3 normal = Vector3.up;
            bool found = false;

            // (1) Onde dikey yuzey (masa, kutu raf, arac kasa yanlari)
            if (Physics.Raycast(chest, transform.forward, out RaycastHit face,
                                 maxForward + 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                point = face.point + face.normal * 0.03f;
                normal = face.normal;
                found = true;
            }

            // (2) Yoksa onun altindaki zemin
            if (!found)
            {
                if (Physics.Raycast(ahead + Vector3.up * 2.2f, Vector3.down,
                                    out RaycastHit ground, 2.2f + maxDown, ~0,
                                    QueryTriggerInteraction.Ignore))
                {
                    point = ground.point + ground.normal * 0.02f;
                    normal = ground.normal;
                    found = true;
                }
            }

            if (!found || !TryFindFreeSpot(point, normal, half, out Vector3 spot))
            {
                LastMessage = "Buraya kargo birakilamaz.";   // #17
                return false;                                 // kutu ELDE kalir
            }

            PlaceAt(_held, spot);

            _held.AttachIgnore(null, null);
            if (_manifest != null) _manifest.NoteReleasedByPlayer(_held.Parcel);
            _held.Parcel.TryChangeState(ParcelState.World);
            _held.ApplyPhysicsForState(ParcelState.World);
            LastMessage = "Kargo birakildi: " + _held.Parcel.ShortLabel;
            _held = null;
            return true;
        }

        /// <summary>
        /// Kutu cevreye gomulmemeli (#74). OverlapBox ile "iÃƒÂ§inde
        /// bir sey var mi" kontrol edilir; sÃ„Â±kÃ„Â±Ã…Å¸Ã„Â±ksa yanlara doÃ„Å¸ru
        /// kaydÃ„Â±rÃ„Â±lÃ„Â±r.
        /// </summary>
        private bool TryFindFreeSpot(Vector3 point, Vector3 normal, Vector3 half,
                                     out Vector3 result)
        {
            result = point;

            // ONCEKI HATA: OverlapBox kontrolu oyuncunun KENDI
            // CharacterController'ini da sayiyordu. Kutu bÃ„Â±rakma
            // noktasi oyuncunun 0.9 m onunde ama govdesi yaricap
            // 0.32 + kutu yaricap oldugu icin her zaman "engelli"
            // cikiyordu ve hicbir kargo birakilamiyordu.
            var self = new List<Collider>();
            GetComponentsInChildren(true, self);

            bool Free(Vector3 p)
            {
                Vector3 up = Mathf.Abs(normal.y) > 0.7f ? Vector3.forward : Vector3.up;
                Vector3 c = p + up * (half.y * 0.5f + 0.01f);

                Collider[] hits = Physics.OverlapBox(
                    c, half * 0.92f, Quaternion.identity, ~0,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < hits.Length; i++)
                {
                    if (self.Contains(hits[i])) continue;   // kendimizi sayma
                    if (hits[i] == null) continue;
                    // Zemin altinda kalmasina izin ver ama icinde olmasina
                    if (IsGroundLike(hits[i])) continue;
                    return false;
                }
                return true;
            }


            if (Free(point)) return true;

            for (float a = 0.35f; a <= 1.6f; a += 0.35f)
            {
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI * 0.25f;
                    Vector3 side = transform.right * (Mathf.Cos(ang) * a)
                                 + transform.forward * (Mathf.Sin(ang) * a);
                    Vector3 p = point + side;
                    // yine zemine otur
                    if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down,
                                        out RaycastHit h, 3f, ~0,
                                        QueryTriggerInteraction.Ignore))
                    {
                        p = h.point + h.normal * 0.02f;
                        if (Free(p)) { result = p; return true; }
                    }
                }
            }
            return false;
        }

        private void PlaceAt(ParcelWorldObject box, Vector3 worldPos)
        {
            box.transform.SetParent(null, true);
            box.transform.position = worldPos;
            box.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        /// <summary>
        /// Kutu yere oturacaksa zemin normali icinde yatay bir yuzey
        /// sayilir; onu engel saymamak gerekir.
        /// </summary>
        private static bool IsGroundLike(Collider c)
        {
            if (c == null) return false;
            if (c.bounds.size.y > 1.2f) return false;         // kocaman duvar/pano
            Vector3 n = Vector3.Cross(c.transform.right, c.transform.forward).normalized;
            return Mathf.Abs(n.y) > 0.6f;                     // yatay yuzey
        }

        // ===============================================================
        // ARAÃƒâ€¡ (#33, #41, #46, #47, #103)
        // ===============================================================
        /// <summary>
        /// Eldeki kutuyu arac kasa alanina koy. Tek kargo (#122 toplu
        /// yukleme YASAK).
        ///
        /// KAPASITE ASIMINDA KUTU ELDE KALIR (#41 altin kural).
        /// </summary>
        public bool TryLoadToVehicle(VehicleCargoArea area)
        {
            LastMessage = "";
            if (!HasCargo)
            {
                LastMessage = "Elinde kargo yok.";
                return false;
            }
            if (area == null)
            {
                LastMessage = "Arac kargo alani bulunamadi.";
                return false;
            }

            Parcel p = _held.Parcel;
            if (!ParcelStateRules.CanLoadToVehicle(p.State))
            {
                LastMessage = "Bu kargo araclanamaz.";
                return false;
            }

            if (!area.Fits(p))
            {
                LastMessage = area.RejectionReason(p);   // #42 aciklayici prompt
                return false;                              // kutu ELDE KALIR
            }

            Transform slot = area.FindFreeSlot(transform.position);
            if (slot == null)
            {
                LastMessage = "Kasa dolu.";
                return false;                              // yine ELDE
            }

            p.TryChangeState(ParcelState.LoadedOnVehicle);

            if (_manifest != null) _manifest.NoteLoadedOnVehicle(p);
            int idx = slot.GetSiblingIndex();
            area.Occupy(_held, idx);
            _held.transform.SetParent(slot, false);
            _held.transform.localPosition = Vector3.zero;
            _held.transform.localRotation = Quaternion.identity;
            _held.AttachIgnore(null, null);
            _held.ApplyPhysicsForState(ParcelState.LoadedOnVehicle);

            LastMessage = "Kargo araca kondu: " + p.ShortLabel;
            _held = null;
            return true;
        }

        /// <summary>
        /// Kasadan TEK kargo al (#44, #123 toplu cikarma YASAK).
        /// Oyuncunun BAKTIGI kutu secilir (#45).
        /// </summary>
        public bool TryUnloadFromVehicle(VehicleCargoArea area, ParcelWorldObject chosen)
        {
            LastMessage = "";
            if (area == null || chosen == null)
            {
                LastMessage = "Kasada secilebilir kargo yok.";
                return false;
            }
            if (HasCargo)
            {
                LastMessage = "Once elindeki kargoyu birak.";
                return false;
            }
            if (!ParcelStateRules.CanUnloadFromVehicle(chosen.Parcel.State))
            {
                LastMessage = "Bu kargo kasada degil.";
                return false;
            }

            area.Release(chosen);

            if (_manifest != null) _manifest.NoteCarriedByPlayer(chosen.Parcel);
            chosen.Parcel.TryChangeState(ParcelState.Carried);
            chosen.transform.SetParent(holdAnchor, false);
            chosen.transform.localPosition = Vector3.zero;
            chosen.transform.localRotation = Quaternion.identity;
            chosen.ApplyPhysicsForState(ParcelState.Carried);

            _held = chosen;
            LastMessage = "Kargo eline alindi: " + chosen.Parcel.ShortLabel;
            return true;
        }

/// <summary>
        /// Kutu NPC'ye TESLIM EDILDI: tasima biter, el bosalir.
        ///
        /// `TryDrop` DEGILDIR: kutu yere birakilmaz, zaten NPC'nin
        /// eline gecmistir. Sadece referans temizlenir.
        /// Ayrica manifest listesinden de cikarilir, yoksa rota eski
        /// kargoyu takip etmeye devam eder.
        /// </summary>
        public bool HandOffDelivered(ParcelWorldObject box)
        {
            if (box == null) return false;
            if (_manifest != null) _manifest.NoteDeliveredFromPlayer(box.Parcel);
            if (_held != box) return false;
            _held = null;
            return true;
        }
        // ===============================================================
        // HIZ (#14, #149)
        // ===============================================================
        public float HoldSpeedMultiplier => holdSpeedMultiplier;
        public float HoldRunMultiplier => holdRunMultiplier;

        // ===============================================================
        // GUARD'LAR (#133, #134)
        // ===============================================================
        private bool _busy;
        /// <summary>
        /// Ayni islem iki kez tetiklenmesin. Pickup animasyonu veya
        /// snap gecisi basladiginda E tekrar basilirsa islem yine
        /// baslatilmaz.
        /// </summary>
        public bool TryBeginOperation()
        {
            if (_busy) return false;
            _busy = true;
            return true;
        }
        public void EndOperation() => _busy = false;
        public bool IsBusy => _busy;

        /// <summary>
        /// Oyuncu respawn/reset oldugunda kutu HAVAYA SACILMAMALI
        /// (#86). Elde kutu varsa guvenli yere birak.
        /// </summary>
        public void SafeDropOnReset()
        {
            if (!HasCargo) return;
            TryDrop();
            if (HasCargo) PlaceAt(_held, transform.position + transform.forward * 1.2f);
            _held = null;
        }
    }
}
