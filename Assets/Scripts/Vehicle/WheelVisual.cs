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
        private WheelCollider wheelCollider;
        private Transform vehicleRoot;

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
                transform.SetPositionAndRotation(position, rotation);
                return;
            }

            transform.localPosition = vehicleRoot.InverseTransformPoint(position);
            transform.localRotation = Quaternion.Inverse(vehicleRoot.rotation) * rotation;
        }
    }
}
