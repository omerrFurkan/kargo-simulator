using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KargoSim.Tests
{
    /// <summary>
    /// Paketleme masasinin (sahne + gorsel katman) etkilesim kurallari.
    ///
    /// PackagingSystemTests saf C# kurallari kilitler. Buradaki testler o
    /// kurallarin OYUNCUNUN ELINDE nasil tetiklendigini korur: kurye E'ye
    /// basar, adim ilerler, ipucu duruma gore degisir.
    ///
    /// Saat elle surulur: PackingTable'a <c>Func&lt;float&gt;</c> olarak
    /// verilen zamanlayici testte kendi sayacimiza baglanir. Boylece test
    /// "1.2 saniye bekle" yazmadan, o saati elle ilerleterek ayni sonuca
    /// ulasir. Gercek zamana (Time.time) bagli bir test, hizli calisan
    /// bir makinede de kalibrasyonsuz kalir.
    /// </summary>
    public sealed class PackingTableTests
    {
        private ParcelType _type;
        private PackagingSystem _packaging;
        private CourierController _courier;
        private PackingTable _table;

        private float _now;
        private int _nextId = 1;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _type = ScriptableObject.CreateInstance<ParcelType>();
            _type.displayName = "Test Kargo";
            _type.minWeightKg = 1f;
            _type.maxWeightKg = 5f;
            _type.minVolumeLiters = 2f;
            _type.maxVolumeLiters = 10f;
            _type.paymentPerKg = 50f;
            _type.color = new Color(0.3f, 0.6f, 0.9f);
            _type.Sanitize();

            _packaging = new PackagingSystem(true);
            _now = 100f;
            _nextId = 1;

            _courier = Spawn("Kurye").AddComponent<CourierController>();

            // Kurye aracla baslar (_inVehicle = true). Paketleme masasi
            // yalnizca kurye AYAKTAyken calisir.
            _courier.ExitVehicle(Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);

            _spawned.Clear();

            if (_type != null) Object.DestroyImmediate(_type);
        }

        private GameObject Spawn(string objectName)
        {
            var go = new GameObject(objectName);
            _spawned.Add(go);
            return go;
        }

        /// <summary>Masa olusturur ve sistemlere baglar (saat teste ait).</summary>
        private PackingTable CreateTable()
        {
            _table = Spawn("PackingTable").AddComponent<PackingTable>();

            // visualRoot null: gorsel yalnizca oyun ici kurulur, testin
            // konusu etkilesim kuralidir.
            _table.Configure(_packaging, _courier, null, () => _now);
            return _table;
        }

        private Parcel NewParcel()
        {
            int id = _nextId++;
            return new Parcel(id, _type, "Ali Veli", 0, 2f, 4f,
                              ParcelPriority.Normal, 300f, 200);
        }

        /// <summary>Kuryeye kutu verir (gorsel yigin dahil).</summary>
        private void GiveToCourier(Parcel parcel)
        {
            IgnoreEditorDestroyError(() => _courier.PickUp(parcel));
            Assert.AreEqual(1, _courier.CarriedCount,
                "Kurye elinde kutu olmali.");
        }

        /// <summary>Kargoyu bastan sona paketler ve elde tutar.</summary>
        private void PackageFullyInHand(Parcel parcel)
        {
            GiveToCourier(parcel);

            for (int guard = 0; guard < 12 && !parcel.IsPackaged; guard++)
            {
                _now += 10f;
                _table.Interact(_courier);
            }

            Assert.IsTrue(parcel.IsPackaged,
                "Tum adimlar sonrasi kargo hazir olmali.");
        }

        /// <summary>
        /// EditMode'da Object.Destroy yasaktir ("DestroyImmediate kullanin").
        /// CourierController eldeki kutunun GORSELINI Destroy ile sildigi
        /// icin PickUp bu editor kuralina takilir; burada yalnizca o hat
        /// bastirilir. Testin konusu olan GERCEK kargo listesi aynen calisir.
        /// </summary>
        private static void IgnoreEditorDestroyError(System.Action body)
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                body();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        // ------------------------------------------------------------------
        // Etkilesim kapilari
        // ------------------------------------------------------------------

        [Test]
        public void EldeKargoYokkenEtkilesimYok()
        {
            CreateTable();

            Assert.IsFalse(_courier.IsInVehicle, "Kurye ayakta olmali.");
            Assert.AreEqual(0, _courier.CarriedCount, "Elde kargo olmamali.");

            Assert.IsFalse(_table.CanInteract(_courier),
                "Elde kargo yokken masada is yok; etkilesim kurulamamali.");
            Assert.AreEqual("Elden kargon yok", _table.Prompt,
                "Ipucu oyuncunun elinin bos oldugunu soylemeli.");
        }

        [Test]
        public void AracIcindeykenEtkilesimYok()
        {
            CreateTable();
            GiveToCourier(NewParcel());

            _courier.EnterVehicle();
            Assert.IsTrue(_courier.IsInVehicle, "Kurye arac icinde olmali.");

            Assert.IsFalse(_table.CanInteract(_courier),
                "Arac icindeyken masada calisilamaz; kurye ayakta degil.");
            Assert.AreEqual(string.Empty, _table.Prompt,
                "Arac icindeyken masa ipucu gostermemeli.");
        }

        [Test]
        public void PaketlenmisKargoYokkenEtkilesimYok()
        {
            CreateTable();
            Parcel parcel = NewParcel();
            PackageFullyInHand(parcel);

            Assert.IsTrue(parcel.IsPackaged, "Kargo hazir olmali.");
            Assert.AreEqual(1, _courier.CarriedCount, "Paket hala elde olmali.");

            Assert.IsFalse(_table.CanInteract(_courier),
                "Elde hazir kargo varsa masada is yok; oyuncu aracagoturur.");
            Assert.AreEqual("Tum paketler hazir", _table.Prompt,
                "Ipucu isin bittigini soylemeli.");
        }

        // ------------------------------------------------------------------
        // Adim akisi
        // ------------------------------------------------------------------

        [Test]
        public void IlkEBasindaKargoMasayaGirer()
        {
            CreateTable();
            Parcel parcel = NewParcel();
            GiveToCourier(parcel);

            Assert.AreEqual(PackagingStep.None, parcel.PackagingProgress);
            Assert.IsTrue(_table.CanInteract(_courier), "E ile masaya girilmeli.");

            _table.Interact(_courier);

            Assert.IsTrue(_packaging.IsOnTable(parcel),
                "Ilk E'de kargo masaya girmeli.");
            Assert.IsTrue(_packaging.IsTableBusy, "Masa dolu olmali.");
            Assert.AreEqual(parcel.Id, _packaging.TableParcelId);
            Assert.AreEqual(_table.WorkParcelId, parcel.Id,
                "Masa gorseli bu kargoyu gostermeli.");

            // Ilk adimin suresi 0: karton konur konmaz kesilebilir.
            Assert.AreEqual(PackagingStep.SheetPlaced, parcel.PackagingProgress,
                "Ilk E ile ilk adim (SheetPlaced) islenmeli.");
        }

        [Test]
        public void PromptHizayaGoreDegisir()
        {
            CreateTable();
            Parcel parcel = NewParcel();
            GiveToCourier(parcel);

            // Kargo henuz masada degil: oyuncu E'ye basabilir.
            string beforeTouch = _table.Prompt;
            StringAssert.StartsWith("E: ", beforeTouch,
                "Kargo masada degilken ipucu eylemi gostermeli.");
            StringAssert.Contains(PackagingSteps.ActionOf(PackagingStep.None),
                beforeTouch, "Ipucu masadaki isin adini soylemeli.");

            _table.Interact(_courier);
            Assert.AreEqual(PackagingStep.SheetPlaced, parcel.PackagingProgress);

            // Ikinci E de aninda gecer (SheetPlaced suresi 0) -> Cut.
            _table.Interact(_courier);
            Assert.AreEqual(PackagingStep.Cut, parcel.PackagingProgress);

            // Cut 1.2 saniye suruyor: is surerken ipucu kalan sureyi yazar.
            string during = _table.Prompt;
            StringAssert.Contains("...", during,
                "Suren iste ipucu bekledigini belli etmeli.");
            StringAssert.Contains("s)", during,
                "Ipucu kalan sureyi yazmali.");
            StringAssert.DoesNotContain("E: ", during,
                "Adim surerken E yazmamali; oyuncu bosuna basmasin.");
            Assert.AreNotEqual(beforeTouch, during,
                "Ipucu masadaki a asamaya gore DEGISMELI.");

            // Sure dolunca ipucu tekrar "E: ..." olur.
            _now += PackagingSteps.DurationOf(PackagingStep.Cut) + 0.1f;

            string ready = _table.Prompt;
            StringAssert.StartsWith("E: ", ready,
                "Sure dolunca oyuncu E'ye basabilir olmali.");
            StringAssert.Contains(PackagingSteps.ActionOf(PackagingStep.Cut), ready,
                "Ipucu o adimin eylemini soylemeli.");
        }

        [Test]
        public void ButunAdimlarSonrasiKargoHazirOlur()
        {
            CreateTable();
            Parcel parcel = NewParcel();
            GiveToCourier(parcel);

            // Her adim icin: E'ye bas, suresi dolana kadar bekle.
            for (int guard = 0; guard < 12 && !parcel.IsPackaged; guard++)
            {
                float left = _packaging.IsOnTable(parcel)
                    ? _packaging.RemainingSeconds(parcel, _now)
                    : 0f;

                _table.Interact(_courier);
                _now += left + 0.05f;
            }

            Assert.AreEqual(PackagingStep.Labelled, parcel.PackagingProgress);
            Assert.IsTrue(parcel.IsPackaged, "Paket hazir olmali.");
            Assert.AreEqual(ParcelState.Ready, parcel.State,
                "Kargo teslimata hazir olmali (Ready).");
            Assert.IsTrue(parcel.IsLoadable, "Kargo araca yuklenebilir olmali.");
            Assert.AreEqual(1, _packaging.CompletedCount,
                "Gun istatistigine bir paket yazilmali.");

            // Kargo hala ELDE: masa yere birakmadi, akis kesintisiz.
            Assert.AreEqual(1, _courier.CarriedCount,
                "Paketlenen kuru EDE kalmali (yere birakilmamali).");

            Assert.IsFalse(_table.CanInteract(_courier),
                "Paketlenen kargoyla masaya gidilmemeli.");
        }

        // ------------------------------------------------------------------
        // Guvenlik
        // ------------------------------------------------------------------

        [Test]
        public void NullKuryereKarsiCokmez()
        {
            CreateTable();
            GiveToCourier(NewParcel());

            Assert.IsFalse(_table.CanInteract(null),
                "Kuryesiz masada etkilesim kurulamamali.");

            Assert.DoesNotThrow(() => _table.Interact(null),
                "Interact(null) istisna atmamali.");
            Assert.IsFalse(_packaging.IsTableBusy,
                "Kuryesiz cagri masada is baslatmamali.");

            // Yapilandirilmamis masa (Configure hic cagrilmadi) de cokmemeli.
            var unconfigured = Spawn("BosPackingTable").AddComponent<PackingTable>();
            Assert.DoesNotThrow(() =>
            {
                Assert.IsFalse(unconfigured.CanInteract(null));
                Assert.IsFalse(unconfigured.CanInteract(_courier));
                unconfigured.Interact(_courier);
                Assert.AreEqual(string.Empty, unconfigured.Prompt);
            }, "Configure edilmemis masa NullReferenceException atmamali.");
        }

        [Test]
        public void AboneTemizligi()
        {
            CreateTable();
            GiveToCourier(NewParcel());
            _table.Interact(_courier);

            // Sahneden silinir: statik GameEvents kuyrugunda ABONE KALMAMALI.
            // GameEvents statik oldugu ve Domain Reload KAPALI calistirilabildigi
            // icin, birakilmayan abonelik olu nesneyi cagirir ve sonraki
            // testlerde "NullReferenceException" yagmuruna donusur.
            int index = _spawned.IndexOf(_table.gameObject);
            Object.DestroyImmediate(_table.gameObject);
            _spawned.RemoveAt(index);

            Assert.DoesNotThrow(() =>
            {
                Parcel ghost = NewParcel();
                GameEvents.RaiseCargoChanged();
                GameEvents.RaisePackagingStarted(ghost);
                GameEvents.RaisePackagingStepCompleted(
                    ghost, PackagingStep.SheetPlaced, PackagingStep.Cut);
                GameEvents.RaiseParcelPackaged(ghost);
            }, "Silinen masa olaylara abone kalmamali.");
        }
    }
}
