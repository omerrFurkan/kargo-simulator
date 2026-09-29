using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 27 - "bisiklet sola donemiyor, zorlaniyor".
    ///
    /// Bu ASIMETRIK bir hata: ayni direksiyon acisi icin sol ve sag
    /// donus hizi esit olmali. Degilse ya direksiyon yari yolda
    /// engelleniyor ya da denge torku tek yone itiyor.
    ///
    /// Ayni test yatma acisini de olcer: "arac surerken yamuluyor"
    /// serisiyesinde kalan konu.
    /// </summary>
    public class BicycleTurningTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Player.VanCargoPoint _bikePoint;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("TurnCleanup");
            SceneManager.SetActiveScene(fresh);
            yield return null;
            Scene game = SceneManager.GetSceneByName("Game");
            if (game.IsValid() && game.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(game);
                while (!unload.isDone) yield return null;
            }
            yield return null;
            Core.GameEvents.ResetForTests();
        }

        private IEnumerator LoadGame()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null; yield return null; yield return null;
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            _bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            Assert.IsNotNull(_bike, "Bisiklet yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
        }

        private IEnumerator Board()
        {
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null; yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
        }

        private float _lastTurn;

        /// <summary>Verilen direksiyonla gaz verip donme hizini olcer.</summary>
        private IEnumerator MeasureTurn(float steer, string etiket, StringBuilder sb)
        {
            // Her olcumde ayni baslangic durumu
            _bike.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, 0f), Quaternion.identity);
            _bike.transform.rotation = Quaternion.identity;
            var body = _bike.GetComponent<Rigidbody>();
            if (body != null) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            // Duz gidecek kadar gaz
            _bike.SetTestInput(0.8f, 0f, 0f, 0f);
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            // Dondur
            _bike.SetTestInput(0.8f, steer, 0f, 0f);
            float yawStart = _bike.transform.eulerAngles.y;
            for (int i = 0; i < 90; i++) yield return new WaitForFixedUpdate();
            float yawEnd = _bike.transform.eulerAngles.y;

            float turned = Mathf.DeltaAngle(yawStart, yawEnd);
            float lean = Mathf.Asin(Mathf.Clamp(
                Vector3.Dot(_bike.transform.right, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;

            sb.AppendLine("  " + etiket +
                " donenDerece=" + turned.ToString("F0") +
                "  yatma=" + lean.ToString("F1"));

            _bike.SetTestInput(0f, 0f, 0f, 0f);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            _lastTurn = turned;
        }

        [UnityTest]
        public IEnumerator SolVeSagDonusSimetrikMi()
        {
            yield return LoadGame();
            yield return Board();

            var sb = new StringBuilder();
            var left = new StringBuilder();
            var right = new StringBuilder();
            float l = 0f, r = 0f;

            yield return MeasureTurn(-1f, "SOL", left);
            l = _lastTurn;

            yield return MeasureTurn(1f, "SAG", right);
            r = _lastTurn;

            sb.AppendLine("BISIKLET DONUSU (a.yi gazli, 90 fizik adimi):");
            sb.Append(left.ToString()).Append(right.ToString());
            Debug.Log("[DONUS]\n" + sb);

            Assert.Greater(Mathf.Abs(l), 8f,
                "Sol direksiyon neredeyse HIC dondurmedi (" + l.ToString("F0") +
                " derece). Oyuncu 'sola donemiyor, zorlaniyor' diyor.");
            Assert.Greater(Mathf.Abs(r), 8f,
                "Sag direksiyon dondurmedi (" + r.ToString("F0") + " derece).");

            float fark = Mathf.Abs(Mathf.Abs(l) - Mathf.Abs(r));
            Assert.Less(fark, Mathf.Max(10f, Mathf.Abs(l) * 0.35f),
                "Donus ASIMETRIK: sol " + l.ToString("F0") + " derece, sag " +
                r.ToString("F0") + " derece (fark " + fark.ToString("F0") +
                "). Oyuncu bir yona donemiyor.");
        }
    }
}
