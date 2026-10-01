using System.Collections.Generic;
using UnityEngine;

namespace KargoSim.Cargo
{
    /// <summary>
    /// DUNYA KARGO KAYDI (kargoMekanigi.md #5B, #84, #85, #117).
    ///
    /// Her ParcelId icin **tek** aktif fiziksel temsil garanti eder.
    ///
    /// ESKI SISTEMDE kutu gorseli her seferinde YENIDEN
    /// `CreatePrimitive` ile olusturuluyordu; kurye 3 kutu tasidiginda 3
    /// ayri obje, aracta her yuklemede yeniden bir obje olusuyordu.
    /// Sonuc: ayni kargonun birden fazla kopyasi (duplicate) ve
    /// kutu sayisi ile gercek kargo sayisinin ayrilmasi.
    ///
    /// BURADA: bir ParcelId -> en fazla bir ParcelWorldObject.
    /// </summary>
    public sealed class CargoWorldRegistry : MonoBehaviour
    {
        private static CargoWorldRegistry _instance;

        public static CargoWorldRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CargoWorldRegistry");
                    _instance = go.AddComponent<CargoWorldRegistry>();
                }
                return _instance;
            }
        }

        private readonly Dictionary<int, ParcelWorldObject> _byParcelId =
            new Dictionary<int, ParcelWorldObject>();

        private static Mesh _boxMesh;
        private static readonly Dictionary<Color, Material> _mats =
            new Dictionary<Color, Material>();

        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _byParcelId)
                    if (kv.Value != null) n++;
                return n;
            }
        }

        /// <summary>Bu kargo icin fiziksel kutu var mi? (#84)</summary>
        public bool Exists(int parcelId) => _byParcelId.ContainsKey(parcelId);

        public ParcelWorldObject Find(int parcelId)
        {
            ParcelWorldObject b;
            return _byParcelId.TryGetValue(parcelId, out b) ? b : null;
        }

        public IEnumerable<ParcelWorldObject> All
        {
            get
            {
                foreach (var kv in _byParcelId)
                    if (kv.Value != null) yield return kv.Value;
            }
        }

        /// <summary>
        /// Kargo icin fiziksel kutu bul veya olustur. Ayni ParcelId icin
        /// ASLA ikinci obje uretmez.
        /// </summary>
        public ParcelWorldObject GetOrCreate(Parcel parcel, Vector3 position,
                                             Transform parent = null)
        {
            if (parcel == null) return null;

            ParcelWorldObject existing = Find(parcel.Id);
            if (existing != null) return existing;

            if (_boxMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tmp.hideFlags = HideFlags.HideAndDontSave;
                _boxMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            }

            var go = new GameObject("Kargo_" + parcel.Id, typeof(MeshFilter),
                                    typeof(MeshRenderer), typeof(BoxCollider),
                                    typeof(Rigidbody), typeof(ParcelWorldObject));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;

            var mf = go.GetComponent<MeshFilter>();
            mf.sharedMesh = _boxMesh;

            var box = go.GetComponent<ParcelWorldObject>();
            box.Bind(parcel);
            go.GetComponent<MeshRenderer>().sharedMaterial = MatFor(parcel);
            go.layer = LayerCargo;

            // Kutu kendi agirligini kendi boyutundan alir.
            var rb = go.GetComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.3f, parcel.WeightKg);

            _byParcelId[parcel.Id] = box;
            return box;
        }

        public const int LayerCargo = 14;

        /// <summary>Kayit defterinden cikar ama NESNEYI SILMEZ.</summary>
        public void Forget(int parcelId)
        {
            _byParcelId.Remove(parcelId);
        }

        /// <summary>
        /// ALTIN KURAL (#87): kutu yalnizca teslim/iptal sonrasi yok
        /// edilir. Bu metod sadece o ani temizler ve kaydi tutmaya
        /// devam eder.
        /// </summary>
        public void DestroyWorldObject(Parcel parcel)
        {
            if (parcel == null) return;
            ParcelWorldObject b = Find(parcel.Id);
            if (b == null) return;

            _byParcelId.Remove(parcel.Id);
            if (Application.isPlaying) Destroy(b.gameObject);
            else DestroyImmediate(b.gameObject);
        }

        /// <summary>
        /// Spawn noktasi guvenli mi? (#85: baska objenin icinde kargo
        /// spawn etme). Kutu yaricapinda bir sey varsa gecersiz.
        /// </summary>
        public static bool IsSpotFree(Vector3 center, Vector3 size)
        {
            Vector3 half = size * 0.5f;
            return !Physics.CheckBox(center + Vector3.up * (half.y + 0.05f),
                                     half * 0.9f, Quaternion.identity,
                                     ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Kutu rengi turden ture degisir (#92) ama "gokkusagi" gibi
        /// her kutu farkli olmaz: uc aile tonu kullanilir, gercek kargo
        /// hissi korunur.
        /// </summary>
        private static Material MatFor(Parcel parcel)
        {
            float t = parcel.VolumeLiters;
            Color c;
            if (t <= 3.5f) c = new Color(0.86f, 0.82f, 0.70f);        // belge/ilac - acik
            else if (t <= 14f) c = new Color(0.72f, 0.56f, 0.34f);   // orta - karton
            else c = new Color(0.52f, 0.40f, 0.28f);                 // buyuk - koyu

            Material m;
            if (_mats.TryGetValue(c, out m) && m != null) return m;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard");
            m = new Material(sh) { name = "Kutu_" + ColorUtility.ToHtmlStringRGB(c) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            _mats[c] = m;
            return m;
        }

        /// <summary>Gun sonu: teslim edilmemis kargolar dunyada kalir.</summary>
        public void ClearAllForNewDay()
        {
            foreach (var kv in _byParcelId)
            {
                if (kv.Value == null) continue;
                if (Application.isPlaying) Destroy(kv.Value.gameObject);
                else DestroyImmediate(kv.Value.gameObject);
            }
            _byParcelId.Clear();
        }
    }
}
