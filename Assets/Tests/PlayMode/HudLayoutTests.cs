using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 19 - HUD YERLESIMI: EKRANIN KOSELERI.
    ///
    /// Oyuncu: "ekrandaki navigasyon ve diger ibareler ekranin cok
    /// ortasinda, koselere yerlestirmemiz gerek".
    ///
    /// HUD ilk yazildigindan beri sahne YAML'ina BAKILARAK ayarlandi;
    /// olcum yapilmadi. Bu dosya yerlesimi OLÇER ve regresyon testi
    /// olur: kimse bir elemani merkeze geri tasimadan gecemez.
    ///
    /// KURAL: ekranin ORTASINDA kalici HUD elemani olmaz. Sadece
    /// gecici bildirimler (odeme, seviye) merkezde olabilir - oyun
    /// oynanirken goruntuyu kapatir.
    /// </summary>
    public class HudLayoutTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private RectTransform _canvas;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("HudLayoutCleanup");
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

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            Assert.IsNotNull(_root, "CompositionRoot yok.");

            var canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(canvases.Length, 0, "Canvas yok.");
            _canvas = canvases[0].transform as RectTransform;
            Assert.IsNotNull(_canvas, "Canvas RectTransform yok.");
        }

        /// <summary>HUD kokusundeki tum arayuz cocuklari (aktif + pasif).</summary>
        private List<RectTransform> HudElements()
        {
            var result = new List<RectTransform>();
            var all = _canvas.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                // Kapsayici (canvas, tam ekran) ve bos parca sayilmaz.
                if (all[i] == _canvas) continue;
                if (!HasVisibleContent(all[i])) continue;
                result.Add(all[i]);
            }
            return result;
        }

        private static bool HasVisibleContent(RectTransform rt)
        {
            if (rt == null) return false;
            // Metin bos ve cubuk dolu degilse gorsel bir sey yok.
            var text = rt.GetComponent<Text>();
            if (text != null && !string.IsNullOrEmpty(text.text)) return true;

            var img = rt.GetComponent<Image>();
            if (img != null && img.sprite != null) return true;
            if (img != null && img.color.a > 0.01f) return true;

            var fill = rt.GetComponent<Image>();
            if (fill != null && !string.IsNullOrEmpty(fill.name)) { }
            return false;
        }

        /// <summary>Ekranin yuzde 0-100 koordinatinda merkez nokta.</summary>
        private static Vector2 CenterPercent(RectTransform rt, RectTransform canvas)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 centre = (corners[0] + corners[2]) * 0.5f;

            var canvasCorners = new Vector3[4];
            canvas.GetWorldCorners(canvasCorners);
            Vector2 cc = (canvasCorners[0] + canvasCorners[2]) * 0.5f;
            Vector2 size = canvasCorners[2] - canvasCorners[0];

            if (Mathf.Abs(size.x) < 0.01f || Mathf.Abs(size.y) < 0.01f) return new Vector2(50f, 50f);

            return new Vector2(
                ((centre.x - cc.x) / size.x + 0.5f) * 100f,
                ((centre.y - cc.y) / size.y + 0.5f) * 100f);
        }

        private string Report(List<RectTransform> elements, RectTransform canvas)
        {
            var sb = new StringBuilder();
            sb.AppendLine("HUD YERLESIMI (ekran yuzde, 0=sol/alt 100=sag/ust):");
            for (int i = 0; i < elements.Count; i++)
            {
                RectTransform rt = elements[i];
                Vector2 p = CenterPercent(rt, canvas);

                var text = rt.GetComponent<Text>();
                string content = text != null ? "\"" + Truncate(text.text) + "\"" : "[cubuk]";

                sb.AppendLine(string.Format("  {0,-14} x={1,6:F1} y={2,6:F1}  {3}",
                    Truncate(rt.name), p.x, p.y, content));
            }
            return sb.ToString();
        }

        private static string Truncate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length <= 22 ? s : s.Substring(0, 22) + "..";
        }

        /// <summary>Ekranin merkez bolgesinde duran kalici HUD elemanlari.</summary>
        private List<string> BlockersInCentre(List<RectTransform> elements, RectTransform canvas)
        {
            var blockers = new List<string>();
            for (int i = 0; i < elements.Count; i++)
            {
                // Modal paneller merkezde OLMALI: oyuncuya bilincli olarak
                // durdurma yaparlar (kod girisi, depo, gun ozeti). Onlar
                // gorus alanini kapatir - bu istenen davranis.
                if (IsInsideModal(elements[i])) continue;

                Vector2 p = CenterPercent(elements[i], canvas);
                // Merkez %30 x %30 alan: gors alaninin govdesi.
                if (p.x > 35f && p.x < 65f && p.y > 35f && p.y < 65f)
                    blockers.Add(elements[i].name + " (x=" + p.x.ToString("F0") +
                                 ", y=" + p.y.ToString("F0") + ")");
            }
            return blockers;
        }

        /// <summary>Modal bir panelin icinde mi? (merkezde olmasi SERBEST)</summary>
        private static readonly string[] ModalRoots =
        {
            "CodeEntry", "DepotPanel", "StopPanel", "DaySummary", "Summary"
        };

        private static bool IsInsideModal(RectTransform rt)
        {
            // Duz Transform uzerinden yuruyoruz: modal kokleri ("CodeEntry",
            // "DepotPanel"...) `new GameObject(...)` ile kuruldugu icin
            // RectTransform DEGIL. Sadece RectTransform'a yuruyen bir
            // arama bu koklere hic ulasamaz ve modal cocuklarini kalici
            // HUD sanardi.
            var node = (Transform)rt;
            int guard = 0;
            while (node != null && guard++ < 40)
            {
                for (int i = 0; i < ModalRoots.Length; i++)
                    if (node.name == ModalRoots[i]) return true;
                node = node.parent;
            }
            return false;
        }

        // ==================================================================
        // Testler
        // ==================================================================

        [UnityTest]
        public IEnumerator HudElemanlariListelenebiliyor()
        {
            yield return LoadGame();

            var elements = HudElements();
            Debug.Log(Report(elements, _canvas));

            Assert.Greater(elements.Count, 3,
                "HUD'da yeterli eleman bulunamadi. HudView baglantilari " +
                "kaybolmus olabilir - para/saat/kapasite gorunmuyor.");
        }

        [UnityTest]
        public IEnumerator KaliciHudElemaniEkraninOrtasindaDegil()
        {
            yield return LoadGame();

            var canvas = _canvas;
            var elements = HudElements();
            string report = Report(elements, canvas);
            Debug.Log(report);

            var blockers = BlockersInCentre(elements, canvas);
            Assert.IsEmpty(blockers,
                "Ekranin ORTASINDA duran kalici HUD elemanlari var. " +
                "Oyuncu gorusu burasi; para/saat/kapasite/rota burada " +
                "durunca oynanabilir degil. Koselere tasinmali." +
                "\n" + report);
        }

        [UnityTest]
        public IEnumerator IstatistiklerSolUstteDurur()
        {
            yield return LoadGame();
            var canvas = _canvas;
            var elements = HudElements();

            // Para / seviye / saat metinlerini bul.
            var money = FindTextContaining(elements, "TL");
            Assert.IsNotNull(money,
                "Para metni bulunamadi ('TL' icermeli). HUD para gostermiyor.");

            Vector2 p = CenterPercent(money, canvas);
            Assert.Less(p.x, 35f,
                "Para metni x=" + p.x.ToString("F0") + " (sol ust olmali, x<35). " +
                "Ekran merkezinde duruyor.");
            Assert.Greater(p.y, 65f,
                "Para metni y=" + p.y.ToString("F0") + " (ust kisim olmali, y>65).");
        }

        private static RectTransform FindTextContaining(List<RectTransform> elements, string needle)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                var t = elements[i].GetComponent<Text>();
                if (t != null && t.text.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return elements[i];
            }
            return null;
        }
    }
}
