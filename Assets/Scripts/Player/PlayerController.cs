using System;
using KargoSim.Vehicle;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Klavye girdisini okur ve araca aktarir.
    ///
    /// Klavye kodunun TEK yeri burasi. Key rebinding ileride gerekirse
    /// sadece bu dosya degisir; arac fiziği hic etkilenmez.
    ///
    /// Girdi eski Input Manager ile okunur (proje `Input System`
    /// paketini icermiyor - bkz. Packages/manifest.json). Bu yuzden
    /// InputManager.asset'te ek axis tanimlamaya gerek yok.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour, IVehicleInput
    {
        [Header("Kontroller")]
        [SerializeField] private KeyCode forwardKey = KeyCode.W;
        [SerializeField] private KeyCode backwardKey = KeyCode.S;
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
        [SerializeField] private KeyCode respawnKey = KeyCode.R;

        [Header("Alternatif Tuslar")]
        [Tooltip("Ok tuslari da calissin mi?")]
        [SerializeField] private bool arrowKeysEnabled = true;

        /// <summary>Cevirme tusu ile kurtarma istendi (tetikleyen bu event'i dinler).</summary>
        public event Action RespawnRequested;

        public float Throttle { get; private set; }
        public float Steer { get; private set; }
        public bool Handbrake { get; private set; }

        private void Update()
        {
            ReadAxes();
            HandleRespawnKey();
        }

        private void ReadAxes()
        {
            float throttle = 0f;
            if (IsDown(forwardKey)) throttle += 1f;
            if (IsDown(backwardKey)) throttle -= 1f;

            float steer = 0f;
            if (IsDown(rightKey)) steer += 1f;
            if (IsDown(leftKey)) steer -= 1f;

            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Handbrake = IsDown(handbrakeKey);
        }

        private void HandleRespawnKey()
        {
            if (Input.GetKeyDown(respawnKey))
                RespawnRequested?.Invoke();
        }

        private bool IsDown(KeyCode key)
        {
            if (Input.GetKey(key)) return true;

            if (!arrowKeysEnabled) return false;

            // Ok tuslari harflerden bagimsiz calsin
            switch (key)
            {
                case KeyCode.W: return Input.GetKey(KeyCode.UpArrow);
                case KeyCode.S: return Input.GetKey(KeyCode.DownArrow);
                case KeyCode.A: return Input.GetKey(KeyCode.LeftArrow);
                case KeyCode.D: return Input.GetKey(KeyCode.RightArrow);
                default: return false;
            }
        }
    }
}
