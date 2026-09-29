using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// GORSEL HATA: "karakter harita zemininin altinda kaliyor".
    ///
    /// Bu test OLCER, varsaymaz.
    ///
    /// BULUNAN GERCEK HATA: fizik dogrudu (ayak-zemin farki 0.03 m)
    /// ama GORELEN model zemine 0.57 m gomuluyordu. Sebep: sahnede
    /// `Model` cocugunun `localPosition.y = -0.600` olmasi. Duzeltme
    /// `GameCompositionRoot.AlignCourierModelToFeet` icinde.
    ///
    /// DERS: ilk yazimda raycast kuryenin KENDI CharacterController
    /// kapsulune carpiyor, "zemin" diye bas yuksekligi donuyordu ve
    /// test yanlis olarak gecuyordu.
    /// </summary>
    public class VisualGroundingTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("GroundingCleanup");
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
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            Assert.IsNotNull(_courier, "Kurye yok.");
        }

        /// <summary>
        /// Karakterin GERCEKTEN durdugu zemin yuksekligi.
        ///
        /// Kuryenin kendi collider'i elenir; kalan en yuksek yuzey zemin
        /// sayilir. Aksi halde isin kendi kapsulune carpiyor.
        /// </summary>
        private float GroundY(Vector3 at, Collider ignore)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                at + Vector3.up * 12f, Vector3.down, 60f, ~0,
                QueryTriggerInteraction.Ignore);

            float best = float.NaN;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                if (ignore != null && (c == ignore
                    || c.transform.IsChildOf(ignore.transform)
                    || ignore.transform.IsChildOf(c.transform))) continue;
                if (float.IsNaN(best) || hits[i].point.y > best)
                    best = hits[i].point.y;
            }
            return best;
        }

        private float FootY()
        {
            var cc = _courier.GetComponent<CharacterController>();
            return _courier.transform.position.y - (cc.height * 0.5f - cc.center.y);
        }

        [UnityTest]
        public IEnumerator KuryeZemininUstundeDuruyor()
        {
            yield return LoadGame();

            var cc = _courier.GetComponent<CharacterController>();
            Assert.IsNotNull(cc, "CharacterController yok; ciddi hata.");

            float footY = FootY();
            float groundY = GroundY(_courier.transform.position, cc);

            Assert.IsFalse(float.IsNaN(groundY),
                "Karakterin altinda zemin yok. Oyuncu 'karakter havada/zeminde "
                + "gömülü' diyor; raycast zemini bulamadi.");

            float sink = footY - groundY;
            TestContext.WriteLine("[Olcum] ayak y=" + footY.ToString("F2")
                + "  zemin y=" + groundY.ToString("F2")
                + "  fark=" + sink.ToString("F2") + " m");

            Assert.Less(sink, 0.15f,
                "Karakter zemine " + sink.ToString("F2")
                + " m gömülü (fizik).");
        }

        [UnityTest]
        public IEnumerator KuryeZemindeTutuyor()
        {
            yield return LoadGame();

            for (int i = 0; i < 60; i++) yield return null;   // fiziK otursun

            var cc = _courier.GetComponent<CharacterController>();
            float footY = FootY();
            float groundY = GroundY(_courier.transform.position, cc);

            TestContext.WriteLine("[Olcum-60kare] ayak-zemin fark = "
                + (footY - groundY).ToString("F2") + " m");

            Assert.Less(footY - groundY, 0.15f,
                "60 kare sonra karakter hâlâ zemine gömülü; yerçekimi "
                + "onu yere indirmiyor.");
        }

        /// <summary>
        /// Collider dogrusu olsa bile GOZLE GORULEN model collider'dan
        /// farkli bir yerdeyse oyuncu "kadro yere gomulmus" gorur.
        /// </summary>
        [UnityTest]
        public IEnumerator GorselModelAyakHizasinda()
        {
            yield return LoadGame();
            for (int i = 0; i < 10; i++) yield return null;

            var renderers = _courier.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0,
                "Kuryenin görsel modeli yok; ekranda kurye görünmez.");

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            var cc = _courier.GetComponent<CharacterController>();
            float footY = FootY();

            Vector3 probe = new Vector3(_courier.transform.position.x, b.min.y,
                                        _courier.transform.position.z);
            float groundY = GroundY(probe, cc);
            float sink = float.IsNaN(groundY) ? 0f : b.min.y - groundY;

            TestContext.WriteLine("[Olcum-gorsel] model alt y=" + b.min.y.ToString("F2")
                + "  ayak y=" + footY.ToString("F2")
                + "  model yuksekligi=" + b.size.y.ToString("F2")
                + "  zemine gomulme=" + sink.ToString("F2") + " m");

            Assert.Greater(sink, -0.15f,
                "Kuryenin GÖRÜLEN modeli zemine " + (-sink).ToString("F2")
                + " m GÖMÜLÜ. Collider doğru olsa bile oyuncu böyle "
                + "kadro görüyor ('karakter yerin altında').");
        }
    }
}
