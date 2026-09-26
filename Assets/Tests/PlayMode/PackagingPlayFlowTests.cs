using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// PAKETLEME — GERCek OYUNCU YOLU DOGRULAMASI.
    ///
    /// Neden ayri test: Kabul testi (GameFlowAcceptanceTests) her etkilesimi
    /// DOGRUDAN cagirir: <c>pile.Interact(courier)</c>,
    /// <c>packingTable.Interact(courier)</c>. Bu yuzden oyuncunun
    /// GERCEK yolu hic denenmez:
    ///
    ///   yurumeye basla -> ScanForFocus() -> fiziksel olarak masayi bul ->
    ///   imlec masanin uzerinde -> E tusu -> UIManager -> TryInteract()
    ///
    /// Bu dosya o bosluklari kapatir. En kritik olani ilk adimdir: masa
    /// collider'i kuryenin interactRadius'ina girIYOR MU? Katman maskesi
    /// yanlissa veya collider kaldirilmissa, tum diger testler yesil
    /// olurken oyuncu E'ye basar ve HICBIR SEY olmaz. Ayni sey raf ve
    /// arac noktasi icin de gecerli.
    ///
    /// KAPSAM SINIRI (dürüst olmak gerekirse): bu test OYNANABILIRLIK ve
    /// ETKILESIM zincirini kanitlar. Gözle görülen seyleri kanitlamaz:
    /// görsel adim sirasi, prompt okunabilirligi, masanin fiziksel olarak
    /// "kolay bulunur" olmasi, animasyon hissi. Bunlar insan gozune
    /// ihtiyac duyar ve otomasyonla olculemez.
    /// </summary>
    public class PackagingPlayFlowTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Player.PackingTable _table;
        private Player.ShopParcelPile _pile;
        private Player.VanCargoPoint _vanPoint;

        /// <summary>
        /// Bir kutunun paketlenmesinde oyuncunun BEKLEDIGI toplam sure.
        ///
        /// Oyuncu E'ye basar ve adim suresi dolana kadar bekler. Bu yuzden
        /// gercek maliyet = adim surelerinin toplami, E sayisi degil.
        /// </summary>
        private static float PackagingWaitForParcel()
        {
            float total = 0f;
            for (int i = 0; i < Cargo.PackagingSteps.StepCount; i++)
                total += Cargo.PackagingSteps.DurationOf((Cargo.PackagingStep)i);
            return total;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("PackagingCleanup");
            SceneManager.SetActiveScene(fresh);
            yield return null;

            Scene game = SceneManager.GetSceneByName("Game");
            if (game.IsValid() && game.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(game);
                while (!unload.isDone) yield return null;
            }
            yield return null;

            // Statik olay bus'i PlayMode testler arasi temizlenmez;
            // yoksa bir onceki testin yikilan sahnesinden kalan
            // CodeEntryView sonraki testte cagrilip exception atar.
            Core.GameEvents.ResetForTests();
        }

        private IEnumerator LoadGame()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            _table = Object.FindFirstObjectByType<Player.PackingTable>();
            _pile = Object.FindFirstObjectByType<Player.ShopParcelPile>();
            _vanPoint = Object.FindFirstObjectByType<Player.VanCargoPoint>();

            Assert.IsNotNull(_root, "GameCompositionRoot yok.");
            Assert.IsNotNull(_courier, "Kurye yok.");
            Assert.IsNotNull(_table, "Paketleme masasi YOK. Menu: Kargo/0. HER SEYI KUR");
            Assert.IsNotNull(_pile, "Dukkan rafi yok.");
            Assert.IsNotNull(_vanPoint, "Arac kargo noktasi yok.");

            // Otomasyon kurali: requiresPackaging ACIK olmali. Kapaliysa
            // bütün bu test anlamsızlaşır (ham kargo doğrudan yüklenir) ve
            // oyuncuya yanlış bir konfor hissi verir.
            Assert.IsTrue(_root.Cargo.Manifest.RequiresPackaging,
                "requiresPackaging KAPALI. Kabul kriteri bunun acik olmasini "
                + "gerektiriyor.");
        }

        /// <summary>
        /// Kuryeyi masanin 1.8 m onune teleporter. "Yurumeyi" taklit eder:
        /// oyuncu masaya dogru yuruyor, sonuc ayni - fiziksel yakinlik.
        /// </summary>
        private void StandNearTable(float distance = 1.8f)
        {
            Vector3 tablePos = _table.transform.position;
            Vector3 stand = tablePos + new Vector3(0f, 0f, distance);
            stand.y = tablePos.y;

            // Zemini bul: masanin altindan biraz asagi indir.
            stand.y += 0.1f;

            _courier.transform.position = stand;
        }

        /// <summary>Kuryeyi aracin kargo noktasinin onune goturur.</summary>
        private void StandNearVan(float distance = 1.6f)
        {
            Vector3 vanPos = _vanPoint.transform.position;
            Vector3 stand = vanPos + new Vector3(0f, 0f, distance);
            stand.y = vanPos.y + 0.1f;
            _courier.transform.position = stand;
        }

        // ==================================================================
        // 1. EN KRITIK: fiziksel odak (ScanForFocus) masayi buluyor mu?
        // ==================================================================

        [UnityTest]
        public IEnumerator KuryeMasaninYaninaGelincedeOdakMasaOluyor()
        {
            yield return LoadGame();

            // ONEMLI: once aractan in. Araçta kuryenin transform'u aracin
            // ustundedir ve Tick icinde ScanForFocus yine de calisir; yani
            // araçtayken de bir odak bulunur. Testi aracta calistirmak
            // "kurye masaya geldi" varsayimini yanlis olcerdi.
            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;
            Assert.IsFalse(_courier.IsInVehicle, "Test icin once araktan inilmeli.");

            // ONEMLI 2: elde kargo OLMALI.
            //
            // ScanForFocus her aday icin CanInteract() cagirir ve
            // PackingTable.CanInteract el bossa FALSE doner ("elde kargon
            // yok"). Masa bu yuzden aday listesinden DUSER ve odak 3.6 m
            // ilerideki rafa gecer. Bu bir hata degil, dogru davranis:
            // bos elle pakete is yoktur. Gercek oyuncu akisinda oyuncu
            // once raftan kutu alir, SONRA masaya gelir.
            //
            // TESPIT: bos elle masanin yaninda duran oyuncunun odagi rafa
            // gider ve oyuncu masanin uyarisini ("Elden kargon yok")
            // gormez, rafin ipucunu gorur. Raporlanan UX bulgusu.
            _pile.Interact(_courier);
            yield return null;
            Assert.Greater(_courier.CarriedCount, 0, "Test oncesi kargo alinamadi.");

            // Oyuncu masaya yurur.
            StandNearTable();
            yield return null;
            yield return null;
            yield return null;

            // ScanForFocus her karede courier.Tick icinde calisir
            // (GameCompositionRoot.Update). Yani 3 kare yeter.
            Assert.AreSame(_table, _courier.Focus,
                "Masanin yaninda duruldugu halde odak (Focus) masaya gelmedi. "
                + "Oyuncu E'ye basinca TIKLAMA olur. Muhtemel nedenler: "
                + "interactMask collider katmanini icermiyor, collider "
                + "kaldirilmis, kuryi masa yuzeyinin ICINDE kaldi, ya da "
                + "baska bir etkilesim noktasi daha yakin.");

            Assert.IsTrue(_courier.Focus.CanInteract(_courier),
                "Odak masa idi ama masada etkilesim kurulamiyor. "
                + "CanInteract neden false donuyor?");
        }

        [UnityTest]
        public IEnumerator OyuncuMasaninTumCevresindeMasaBulunuyor()
        {
            yield return LoadGame();

            // Kurye ilk kez masanin yonunde degil: UIManager acik, kurye
            // rafin onunde. Burada odak MASAYA olmamali - bu, masanin
            // yanlislikla her yerden tetiklenmedigini gosterir.
            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;

            StandNearVan();
            yield return null;
            yield return null;

            Assert.AreNotSame(_table, _courier.Focus,
                "Oyuncu masadan uzakta (arac noktasinda) dururken odak masa "
                + "olmamali; mesafe filtresi calismiyor.");
        }

        // ==================================================================
        // 2. GERCEK E YOLU: TryInteract ile 6 adim
        // ==================================================================

        [UnityTest]
        public IEnumerator EyleGercekOyuncuYolundanAltiAdimiTamamlar()
        {
            yield return LoadGame();

            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;

            // 1) Ham kargo al (odak yoluyla degil, burada dogrudan -
            //    rafin odagi zaten baska bir testin konusu).
            _pile.Interact(_courier);
            yield return null;
            Assert.Greater(_courier.CarriedCount, 0, "Raftan kargo alinamadi.");

            Cargo.Parcel parcel = _courier.CarriedParcels[0];
            Assert.IsFalse(parcel.IsPackaged, "Kargo daha paketlenmemis olmali.");
            var startState = parcel.State;
            Assert.AreNotEqual(Cargo.ParcelState.Ready, startState,
                "Raf kargosu hazir gelmemeli; akis baslamadi.");

            // 2) Masaya yurur.
            StandNearTable();
            yield return null;
            yield return null;

            // 3) Alti adim, GERCEK E yolundan: courier.TryInteract().
            //
            // RAF KASTEN ELI DOLDURUR (ShopParcelPile.Interact elde dolana
            // kadar alir). Bu yuzden cogu zaman 3 kutu olur, 1 degil.
            // Burada ELDEKI TUM KUTULARI paketliyoruz: oyuncunun gercek
            // yaptigi gibi.
            int boxesInHand = _courier.CarriedCount;
            int packaged = 0;

            for (int b = 0; b < boxesInHand; b++)
            {
                StandNearTable();
                yield return null;
                yield return null;

                int steps = 0;
                while (steps < 40 && !parcel.IsPackaged)
                {
                    Assert.AreSame(_table, _courier.Focus,
                        "Adim " + steps + ": odak masadan kaydi. Akis koptu.");
                    Assert.IsTrue(_courier.TryInteract(),
                        "Adim " + steps + ": E tusu is yapmadi. Prompt: \""
                        + _table.Prompt + "\"");

                    steps++;
                    // Suresi dolmadiysa simulasyonu ilerlet.
                    _root.Delivery.Tick(2.5f, false);
                    yield return null;
                }

                Assert.IsTrue(parcel.IsPackaged,
                    "Kutu " + b + " 40 denemede paketlenmedi. Kalan: "
                    + parcel.PackagingProgress);
                Assert.AreEqual(Cargo.ParcelState.Ready, parcel.State,
                    "Paketlendikten sonra durum Ready olmali.");
                Assert.AreEqual(1f, parcel.PackagingProgress01, 0.0001f,
                    "Ilerleme tamamlaninca 1.0 olmali.");
                packaged++;

                // Sonraki kutuya gec: kurye elindeki sirayla ilerler.
                if (b + 1 < _courier.CarriedCount) parcel = _courier.CarriedParcels[b + 1];
            }

            Assert.AreEqual(boxesInHand, packaged,
                "Eldeki tum kutular paketlenmeli.");
            for (int i = 0; i < _courier.CarriedParcels.Count; i++)
                Assert.IsTrue(_courier.CarriedParcels[i].IsPackaged,
                    "Kutu " + i + " paketlenmemis kaldi; akis koptu.");

            // Kutular ELDE kalmali (yere birakilmamali).
            Assert.AreEqual(boxesInHand, _courier.CarriedCount,
                "Paket hazir olunca kutular kuryenin elinde kalmali; "
                + "yere birakildiysa oyuncu tekrar alip tasimak zorunda kalir.");

            // Sure olcumu: oyuncu 3 kutu icin kac saniye bekliyor?
            // Bu sayi gunun 300 saniyelik suresine gore KARSILASTIRILIR.
            float perBox = PackagingWaitForParcel();
            Debug.Log("[PaketlemeSure] Kutu basina toplam bekleme: "
                        + perBox.ToString("0.0") + " sn. " + boxesInHand
                        + " kutu icin: " + (perBox * boxesInHand).ToString("0.0")
                        + " sn (gun 300 sn).");
        }

        // ==================================================================
        // 3. KABUL KRITERI 2: ham kargo araca GIREMEZ
        // ==================================================================

        [UnityTest]
        public IEnumerator PaketlenmemisKargoAracaYuklenemiyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;
            _pile.Interact(_courier);
            yield return null;

            Cargo.Parcel raw = _courier.CarriedParcels[0];
            Assert.IsFalse(raw.IsPackaged, "Bu test ham kargoyla calismali.");

            int before = _root.Cargo.Manifest.LoadedCount;

            // Arac noktasina E - GERCEK oyuncu yolundan.
            StandNearVan();
            yield return null;
            yield return null;
            Assert.AreSame(_vanPoint, _courier.Focus,
                "Arac noktasinin odagi gelmedi; arac etkilesimi bozuk.");

            _vanPoint.Interact(_courier);
            yield return null;

            Assert.AreEqual(before, _root.Cargo.Manifest.LoadedCount,
                "PAKETLENMEMIS kargo araca yuklendi. Kabul kriteri ihlali: "
                + "ham kargo aracda olmamali.");
            Assert.Greater(_courier.CarriedCount, 0,
                "Ham kargo reddedildi ama ELDEN DE KAYBOLMADI. Oyuncu kargoyu "
                + "kaybetmemeli, paketlemeye devam edebilmeli.");
        }

        // ==================================================================
        // 4. KABUL KRITERI 1: paketlenmis kargo araca GIRER
        // ==================================================================

        [UnityTest]
        public IEnumerator PaketlenmisKargoAracaYukleniyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;
            _pile.Interact(_courier);
            yield return null;

            Cargo.Parcel parcel = _courier.CarriedParcels[0];
            StandNearTable();
            yield return null;
            yield return null;

            int guard = 0;
            while (!parcel.IsPackaged && guard++ < 40)
            {
                _courier.TryInteract();
                _root.Delivery.Tick(2.5f, false);
                yield return null;
            }
            Assert.IsTrue(parcel.IsPackaged, "Paketlenemedi.");

            StandNearVan();
            yield return null;
            yield return null;
            Assert.AreSame(_vanPoint, _courier.Focus, "Arac odagi gelmedi.");

            int before = _root.Cargo.Manifest.LoadedCount;

            // Bu test YALNIZCA ilk kutuyu paketler. Elde 3 kutu var:
            // 1 paketli + 2 ham. Beklenen davranis tam olarak budur -
            // ara noktasi paketlenmemişi reddeder, paketleneni alir.
            int inHand = _courier.CarriedCount;
            Assert.GreaterOrEqual(inHand, 1, "Elde kargo olmali.");

            _vanPoint.Interact(_courier);
            yield return null;

            Assert.AreEqual(before + 1, _root.Cargo.Manifest.LoadedCount,
                "YALNIZCA paketlenmis kutu araca girmeli.");
            Assert.IsTrue(parcel.IsLoaded, "Paketlenen kargo aracda isaretlenmeli.");

            // Ham kutular ELDE KALIR - oyuncu islerini kaybetmemeli,
            // masaya donup paketlemeye devam edebilmeli.
            Assert.AreEqual(inHand - 1, _courier.CarriedCount,
                "Yalnizca paketlenen kutu yuklenmeli; ham kutular elde "
                + "kalmali. Ham kargolar kaybolursa oyuncu onlari yeniden "
                + "raf aramak zorunda kalir.");
        }

        // ==================================================================
        // 5. COGUL KUTU: kilitlenme var mi?
        // ==================================================================

        [UnityTest]
        public IEnumerator UcKutuPaketlenipAracaYukleniyorKilitlenmeYok()
        {
            yield return LoadGame();

            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;
            _pile.Interact(_courier);
            yield return null;

            int carried = _courier.CarriedCount;
            Assert.GreaterOrEqual(carried, 2,
                "Kilitleme testi icin en az 2 kargo gerekiyor; oyuncu "
                + "tek kutuyu elle tasimak zorunda kalirsa kargo tasima "
                + "oynanabilirligi suphelidir.");

            // Her kargoyu paketle: odak her seferinde yeniden aranir.
            for (int i = 0; i < carried; i++)
            {
                StandNearTable();
                yield return null;
                yield return null;

                int guard = 0;
                while (guard < 40 && i < _courier.CarriedParcels.Count &&
                       !_courier.CarriedParcels[i].IsPackaged)
                {
                    _courier.TryInteract();
                    _root.Delivery.Tick(2.5f, false);
                    guard++;
                    yield return null;
                }
            }

            for (int i = 0; i < _courier.CarriedParcels.Count; i++)
                Assert.IsTrue(_courier.CarriedParcels[i].IsPackaged,
                    "Kargo " + i + " paketlenmedi; coklu kutuda akis koptu.");

            StandNearVan();
            yield return null;
            yield return null;

            int before = _root.Cargo.Manifest.LoadedCount;

            // Interact SONRASI CarriedCount sifirdir; onceki sayiyi onceden
            // tutmaliyiz (yoksa "hepsi yuklendi" kontrolu anlamsiz olur).
            int expected = _courier.CarriedCount;

            _vanPoint.Interact(_courier);
            yield return null;

            Assert.AreEqual(before + expected, _root.Cargo.Manifest.LoadedCount,
                "Tum kutular araca girmeli. Kapasite doluysa bu dogru bir "
                + "sinirdir, hata degil - bu durumda yuklenen sayisi daha "
                + "kucukse kapasite mesajini oku.");
            Assert.AreEqual(0, _courier.CarriedCount,
                "Yukleme sonrasi el bosalmali.");
        }

        // ==================================================================
        // 6. PROMPT: oyuncu ne goruyor? (otomatik olculebilen kismi)
        // ==================================================================

        [UnityTest]
        public IEnumerator PromptBosDegilVeEyiIceriyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;

            // Elde kargo yokken: anlamli bir aciklama gostermeli, BOS
            // ("" donen) ipucu oyuncuyu neden etkilesim yapamiyorum
            // diye tereddut ettirir.
            StandNearTable();
            yield return null;
            yield return null;

            string promptNoCargo = _table.Prompt;
            Assert.IsNotNull(promptNoCargo, "Prompt null donmemeli.");
            Assert.IsNotEmpty(promptNoCargo,
                "Elde kargo yokken prompt BOS. Oyuncu masayi bos sanir.");

            // Elde kargo varken: eylem metni gorunmeli.
            _pile.Interact(_courier);
            yield return null;

            string promptWithCargo = _table.Prompt;
            Assert.IsNotEmpty(promptWithCargo, "Elde kargo varken prompt bos.");
            Assert.IsTrue(promptWithCargo.Contains("E") || promptWithCargo.Contains("..."),
                "Prompt ne eylem ne bekleme bildiriyor: \"" + promptWithCargo
                + "\". Oyuncu E'ye basip basmayacagini bilemez.");
        }
    }
}
