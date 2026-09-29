using UnityEngine;

namespace KargoSim.Vehicle
{
    /// <summary>
    /// Bir WheelCollider'in gorsel mesh'ini fizik pozuna baglar.
    ///
    /// Neden gerekli: WheelCollider yalnizca fizik icindir, gorunmez.
    /// Onune konan mesh kendi basina ne doner ne de direksiyonu takip
    /// eder; arac ilerlerken tekerler havada duruyormus gibi gorunur.
    ///
    /// WheelCollider.GetWorldPose, tekerin DONME ve DIREKSIYON
    /// durumunu tek bir poz olarak verir. Bu yuzden tek atama ile iki
    /// sorun birden cozulur; ayri bir "donme hizi" tutmak gereksiz.
    ///
    /// Eksen duzeni: Unity tekeri local +Z yonunde yuvarlar, donme ekseni
    /// local X'tir. Kenney tekerleri zaten bu duzende modellenmistir,
    /// bu yuzden ek donusum (rotation) uygulanmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WheelVisual : MonoBehaviour
    {
        [Tooltip("Mesh'in durus ofseti. Unity SILINDIR primitifleri icin 90 " +
                 "(dik dururlar), Kenney teker modelleri icin 0 (zaten dikeyler). " +
                 "Varsayilan 0: yanlislikla tum modellere 90 uygulamak, " +
                 "dogru modellenmis tekerleri YATIRIR.")]
        [SerializeField] private Vector3 restEuler = Vector3.zero;


        private WheelCollider wheelCollider;
        private Transform vehicleRoot;

        /// <summary>
        /// ASAMA 23: FIZIK BAGLANTISI OTOMATIK KURULUR.
        ///
        /// Oyuncu fotograf gonderdi: tekerler yere yatmis ve aracdan kopmus
        /// gorunuyordu. Sebep iki katmanliydi:
        ///
        ///  1) `wheelCollider` serialize EDILMEMISTI. `Bind()` yalnizca
        ///     editor'de (prefab uretimi) cagriliyordu; runtime'da null
        ///     kaliyor ve `Sync()` hic calismiyordu. Yani tekerler oyunda
        ///     hic donmuyor, direksiyonu takip etmiyordu.
        ///  2) Prefab uretimi sirasinda `Sync()` cagrilip mesh'in 90
        ///     derecelik durus ofsetini SILMIS ve kaydetmis. Prefaba oyle
        ///     yazildigi icin lastik yatik kaydedilmis olarak duruyordu.
        ///
        /// Artik `Awake` baglantiyi kendisi kuruyor: teker kendi ebeveynini
        /// bulur. Editor'da uretilen prefab da calisir, elle kurulan da.
        /// </summary>
        private void Awake()
        {
            if (wheelCollider == null)
                wheelCollider = GetComponentInParent<WheelCollider>();

            if (vehicleRoot == null)
            {
                var v = GetComponentInParent<Vehicle.IVehicleController>();
                if (v != null && v.VehicleTransform != null) vehicleRoot = v.VehicleTransform;
                else vehicleRoot = transform.root;
            }
        }

        /// <summary>
        /// Editor arac prefabini uretirken cagrilir. Cagrilmazsa mesh
        /// yerinde durur (bu yuzden uretimde cagrilmasi zorunlu).
        /// </summary>
        public void Bind(WheelCollider target, Transform root)
        {
            wheelCollider = target;
            vehicleRoot = root;
            Sync();
        }

        private void LateUpdate()
        {
            Sync();
        }

        private void Sync()
        {
            if (wheelCollider == null) return;

            wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);

            // Gorsel mesh arac kokunun cocugu; world pozunu local'a cevir.
            if (vehicleRoot == null)
            {
                transform.SetPositionAndRotation(position, rotation * RestRotation());
                return;
            }

            transform.localPosition = vehicleRoot.InverseTransformPoint(position);

            // ASAMA 23: DURUS OFFSET'I KORUNMALI - "tekerlerin hali ne".
            //
            // Oyuncu fotograf gonderdi: tekerler yere YATIK duruyor ve
            // aracdan kopmus gorunuyordu.
            //
            // Sebep: Unity silindir primitinin ekseni local Y'dir ve teker
            // ancak 90 derece dondurulunce dik durur (uretimde
            // `Quaternion.Euler(0, 0, 90)` ile kuruluyor). Burada ise mesh'in
            // rotasyonu SADECE fizik pozundan yaziliyordu; 90 derecelik
            // durus ofseti HER KARE siliniyordu. Sonuc: lastik yatay
            // duruyor, ekranda yere yatmis tabak gibi gorunuyordu.
            //
            // Cozum: fizik pozunun ARASINA mesh'in durus ofseti girer.
            // `rotation * rest` - once fizik dondurmesi, sonra mesh ofseti.
            transform.localRotation = Quaternion.Inverse(vehicleRoot.rotation)
                                     * rotation
                                     * RestRotation();
        }

        private Quaternion RestRotation() => Quaternion.Euler(restEuler);

        /// <summary>Editor kurulum kancası: mesh'in durus ofsetini ayarlar.</summary>
        public void ConfigureForSetup(Vector3 euler) => restEuler = euler;
    }
}
