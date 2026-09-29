using System.IO;
using System.Text;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Player;
using KargoSim.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// 3 TEKERLEKLI KARGO BISIKLETI prefabini ve sahne kopyasini uretir.
    ///
    /// NEDEN AYRI BIR KURULUM ARACI: Van, <see cref="VehiclePrefabBuilder"/>
    /// tarafindan kuruluyor ve dort tekerli oldugu icin o uretici
    /// tekerleri DIZI SIRASIYLA iliskilendiriyor ("ilk iki teker direksiyon").
    /// Bisikletin on/arka gecisi farkli; ayni uretici degistirilseydi vanin
    /// yerlesimi de tehlikeye girerdi.
    ///
    /// YENI ASSET YOK: Kenney bisiklet modeli projede yok. Tum gorsel
    /// Cube / Cylinder primitive'lerinden kurulur; ileride gercek model
    /// geldiginde yalnizca gorsel cocuklar degisir, fizik ve baglantilar
    /// aynen korunur.
    ///
    /// KAPASITE IKI SISTEMI BIRLESMIYOR: mevcut
    /// <see cref="VehicleDefinition.cargoCapacityKg"/> ve
    /// <c>cargoCapacityLiters</c> kullanilir. Yeni alan eklenmedi; boylece
    /// CargoSystem.ApplyVehicleCapacity ayni yoldan calisir.
    ///
    /// Menu tekrar calistirilabilir: prefab her seferinde bastan uretilir,
    /// sahnedeki eski arac DestroyImmediate ile temizlenir.
    /// </summary>
    public static class CargoBikeSetupTool
    {
        private const string VehicleDefPath =
            "Assets/ScriptableObjects/Definitions/Vehicle_Bicycle.asset";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = PrefabFolder + "/CargoBike.prefab";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private const string FrameMaterialPath = "Assets/Art/Materials/CargoBikeFrame.mat";
        private const string TireMaterialPath = "Assets/Art/Materials/CargoBikeTire.mat";
        private const string BedMaterialPath = "Assets/Art/Materials/CargoBikeBed.mat";

        private const int LayerVehicle = 11;

        // --- Arac tanimi (bisiklet vansdan kucuk ve yavas) ---
        private const float DefMass = 180f;

        // TUNING (Asama 14) - OLCUMLE bulundu, tahmin degil.
        //
        // BIRIM NOTU: `maxSpeed` motor matematiğinde m/s OLARAK
        // kullanilir (ForwardSpeed = linearVelocity projeksiyonu, m/s).
        // Ilk denemede 22f yazilmis ve bu 22 m/s = 79 km/h demekti;
        // oyuncunun bisikleti "roket" gibi surmesine yol acti.
        //
        // OLCULEN (BicyclePhysicsMeasurements):
        //   maxSpeed 8.5 m/s -> azami 39.4 km/h  (hedef 30, fazla hizli)
        //   maxSpeed 6.8 m/s -> ~30 km/h hedefi
        // Azami hiz tork egrisiyle ulasilmaz (eylemsizlik tasar), bu
        // yuzden 6.8 secildi.
        private const float DefMaxSpeed = 6.8f;        // m/s ≈ 24.5 km/h
        private const float DefMaxReverseSpeed = 2.5f; // m/s ≈ 9 km/h
        private const float DefMaxSteerAngle = 38f;
        private const float DefEngineForce = 1400f;
        private const float DefBrakeForce = 1800f;
        private const float DefHandbrake = 2f;
        private const float DefCargoKg = 60f;
        private const float DefCargoLiters = 150f;
        private const float DefWheelRadius = 0.32f;
        private const float DefSuspensionDistance = 0.15f;
        private const float DefSuspensionSpring = 14000f;
        private const float DefSuspensionDamper = 2200f;
        private const float DefCenterOfMassHeight = 0.32f;
        private const float DefLateralGrip = 8f;
        private const float DefDownforce = 0f;

        // --- Kadro olculeri (yerel, +Z one bakiyor) ---
        private const float FrontAxleZ = 0.78f;
        private const float RearAxleZ = -0.72f;
        private const float RearTrackHalf = 0.20f;

        // Kasa: acik kutuyu andiran cerceve. Ic olculer
        // CargoBikeVisual'in bedSize degeriyle AYNIDIR (0.95 x 1.15 m);
        // iki taraf ayri yazilirsa kutular kasanin disina tasar.
        private const float BedInnerX = 0.95f;
        private const float BedInnerZ = 1.15f;
        private const float BedWall = 0.06f;
        private const float BedBaseY = 1.09f;     // tabanin alt yuzeyi
        private const float BedBaseThickness = 0.06f;
        private const float BedWallHeight = 0.55f;
        private const float BedCenterZ = -0.95f;

        [MenuItem("Kargo/10. Kargo Bisikleti Kur", priority = 9)]
        public static void Build()
        {
            EnsureFolders();

            var report = new StringBuilder();

            VehicleDefinition definition = CreateVehicleDefinition(report);

            Material frameMat = CreateMaterial(FrameMaterialPath, new Color(0.20f, 0.55f, 0.52f));
            Material tireMat = CreateMaterial(TireMaterialPath, new Color(0.12f, 0.12f, 0.14f));
            Material bedMat = CreateMaterial(BedMaterialPath, new Color(0.62f, 0.44f, 0.26f));

            GameObject root = null;
            try
            {
                root = new GameObject("CargoBike");
                root.layer = LayerVehicle;

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = DefMass;
                rb.drag = 0.2f;
                rb.angularDrag = 4f;
                // Interpolate: 50 Hz fizik, 60+ Hz gorsel. Bu olmadan
                // bisiklet titrer (kucuk teker, kisa govde daha belirgin).
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                // Bisiklet duvara carparken "tunnelleyip" gecmesin.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // SERT GOVDE: yalnizca tekerlerden olan bir arac ilk itkide
                // takla atar. Tek bir BoxCollider hem sasiyi hem kasa
                // tabanini kapsar. Kutu ve kasa duvarlari collider'sizdir;
                // boylece kasanin icine girilebilir ve kutular aracin
                // fizigini elle degistirmez.
                var chassis = root.AddComponent<BoxCollider>();
                chassis.center = new Vector3(0f, 0.86f, -0.35f);
                chassis.size = new Vector3(0.80f, 0.46f, 2.20f);

                var controller = root.AddComponent<BicycleController>();

                // --- Uc teker: 1 on (direksiyonlu), 2 arka (sabit) ---
                BuildWheels(root.transform, definition, frameMat, tireMat, report);

                // --- ON DUVAR: kadro borulari, gidon, sele, pedal ---
                BuildFrame(root.transform, frameMat, tireMat);

                // --- ARKA KASA: acik cerceve + bos ic cocuk ---
                Transform bed = BuildCargoBed(root.transform, bedMat);

                // Kutu gorseli kasanin bos cocugunu doldurur. Editor kancasi
                // cagrilir; Inspector'a elle yazmaya gerek yok.
                var visual = root.AddComponent<CargoBikeVisual>();
                visual.ConfigureForSetup(bed);

                // Kargo noktasi: bisikletin kargo kapisi.
                //
                // DIKKAT: VanCargoPoint.Configure(...) CAGIRILMAZ. Imzasi
                // IVehicleController aliyor ve sistemler CompositionRoot'ta
                // dogar; kurulum araci sahne tanimini yapmaz, yalnizca
                // bileseni ve etkilesim collider'ini koyar.
                BuildCargoPoint(root.transform);

                // Awake editor'de calismaz, o yuzden referanslar atanip
                // fizik degerleri ELLE uygulanir. BicycleController'in
                // 'body' alani private oldugu icin SerializedObject ile
                // yazilir (kendi dosyasina dokunmadan).
                AssignPrivateField(controller, "body", rb);
                controller.ConfigureForSetup(definition,
                    FindWheel(root.transform, "Wheel_Front"),
                    FindWheel(root.transform, "Wheel_RearLeft"),
                    FindWheel(root.transform, "Wheel_RearRight"));

                // --- SURUCU KAMERASI (Asama 14) ---
                //
                // Iki arac = iki kamera. Van Camera.main'i kullanir; bisiklet
                // icin ayri bir kamera olmazsa CompositionRoot bisiklete
                // binerken van kamerasini verir ve oyuncu bisikleti
                // 6.5 m yukaridan arkadan izler - kask gibi.
                //
                // Bisiklet kamerasi SURUCU MODUNDA: gidonun arkasinda,
                // kafanin hizinda, aracin rotasyonunu takip eder.
                // Kasa arkada kalir, oyuncu ne yaptigini gorur.
                BuildDriverCamera(root.transform);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError("[KargoBisiklet] Prefab kaydedilemedi: " + PrefabPath);
                    return;
                }
            }
            finally
            {
                // Prefab kaydedildiyse de kaydedilmediyse de gecici nesne
                // her durumda temizlenir.
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            VerifyPrefab(report);
            PlaceInScene(report);

            Debug.Log("[KargoBisiklet] Kurulum tamam.\n" + report);
        }

        // ------------------------------------------------------------------
        // Arac tanimi
        // ------------------------------------------------------------------

        private static VehicleDefinition CreateVehicleDefinition(StringBuilder report)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(VehicleDefPath);
            bool created = def == null;

            if (created)
            {
                def = ScriptableObject.CreateInstance<VehicleDefinition>();
                AssetDatabase.CreateAsset(def, VehicleDefPath);
            }

            // Degerler her calistirmada YAZILIR. Prefab yeniden kuruldugu
            // icin tanim da eski halinde kalmamali; elle bir degeri
            // degistirip menuyi tekrar calistirmak duzeltmek icin yeterli.
            def.mass = DefMass;
            def.maxSpeed = DefMaxSpeed;
            def.maxReverseSpeed = DefMaxReverseSpeed;
            def.maxSteerAngle = DefMaxSteerAngle;
            def.engineForce = DefEngineForce;
            def.brakeForce = DefBrakeForce;
            def.handbrakeStrength = DefHandbrake;
            def.cargoCapacityKg = DefCargoKg;
            def.cargoCapacityLiters = DefCargoLiters;
            def.wheelRadius = DefWheelRadius;
            def.suspensionDistance = DefSuspensionDistance;
            def.suspensionSpring = DefSuspensionSpring;
            def.suspensionDamper = DefSuspensionDamper;
            def.centerOfMassHeight = DefCenterOfMassHeight;
            def.lateralGrip = DefLateralGrip;
            def.downforce = DefDownforce;

            EditorUtility.SetDirty(def);

            report.AppendLine("  tanim       : " + VehicleDefPath
                              + (created ? " (olusturuldu)" : " (guncellendi)"));
            report.AppendLine("  kapasite    : " + DefCargoKg + " kg / " + DefCargoLiters
                              + " L  (van 350 kg / 900 L)");
            report.AppendLine("  max hiz     : " + DefMaxSpeed + " m/s  (azami geri "
                              + DefMaxReverseSpeed + ")");
            report.AppendLine("  teker       : yaricap " + DefWheelRadius + " m, suspansiyon "
                              + DefSuspensionDistance + " m");
            return def;
        }

        // ------------------------------------------------------------------
        // Tekerler
        // ------------------------------------------------------------------

        private static void BuildWheels(Transform parent, VehicleDefinition def,
                                        Material frameMat, Material tireMat, StringBuilder report)
        {
            float r = def.wheelRadius;
            float y = r;   // teker merkezi yerden yaricap kadar yukarida

            // BicycleController uc alani AYRI AYRI isaret eder (on / arka
            // sol / arka sag), dizi sirasiyla degil. Yine de on once
            // kurulur: okunabilirlik.
            CreateWheel("Wheel_Front", new Vector3(0f, y, FrontAxleZ), parent, def, frameMat, tireMat);
            CreateWheel("Wheel_RearLeft", new Vector3(-RearTrackHalf, y, RearAxleZ), parent, def, frameMat, tireMat);
            CreateWheel("Wheel_RearRight", new Vector3(RearTrackHalf, y, RearAxleZ), parent, def, frameMat, tireMat);

            report.AppendLine("  teker sayisi: 3 (1 on + 2 arka)");
        }

        private static WheelCollider CreateWheel(string wheelName, Vector3 localPosition,
                                                 Transform parent, VehicleDefinition def,
                                                 Material frameMat, Material tireMat)
        {
            var wheelGo = new GameObject(wheelName);
            wheelGo.layer = LayerVehicle;
            wheelGo.transform.SetParent(parent, false);
            wheelGo.transform.localPosition = localPosition;

            // WheelCollider yalnizca fizik icindir; gorsel icerik buraya
            // dokunulmaz.
            var collider = wheelGo.AddComponent<WheelCollider>();
            collider.radius = def.wheelRadius;
            collider.suspensionDistance = def.suspensionDistance;

            JointSpring spring = collider.suspensionSpring;
            spring.spring = def.suspensionSpring;
            spring.damper = def.suspensionDamper;
            spring.targetPosition = 0.5f;
            collider.suspensionSpring = spring;

            // Gorsel silindir cocuk nesne olarak: donerken ekseni degismez.
            // Unity silindiri cap 1, YUKSEK 2 ikeylidir: yaricap = cap * 0.5,
            // genislik = yukseklik * 1. Cap = 2 * radius, genislik ince
            // (0.12 m) tutulur; bisiklet lastigi vandan cok daha dardir.
            GameObject visual = CreateCylinder("Mesh",
                Vector3.zero,
                new Vector3(def.wheelRadius * 2f, 0.06f, def.wheelRadius * 2f),
                Quaternion.Euler(0f, 0f, 90f),
                wheelGo.transform, tireMat);

            // Hub: kadro renginde ince bir disk. Tekerin dondugunu gorselde
            // okunur kilar. Lastikten biraz genis (0.16 m) ki kadro
            // borusunun ucu orada bitsin.
            CreateCylinder("Hub", Vector3.zero,
                new Vector3(0.18f, 0.08f, 0.18f),
                Quaternion.Euler(0f, 0f, 90f),
                visual.transform, frameMat);

              // WheelVisual gorseli fizik pozuna baglar: donme + direksiyon
              // otomatik takip eder. Cagrilmazsa tekerler havada durur.
              //
              // ASAMA 23: durus ofseti ACIKÇA verilir. Unity silindiri
              // ancak 90 derece dondurulunce dik durur; bu ofset verilmezse
              // mesh her kare fiziğin pozunu alip YATAY kalir (oyuncunun
              // fotografinda gordugu tabak gibi tekerler).
              var wheelVisual = visual.AddComponent<WheelVisual>();
              wheelVisual.ConfigureForSetup(new Vector3(0f, 0f, 90f));
              wheelVisual.Bind(collider, parent);


            return collider;
        }

        private static WheelCollider FindWheel(Transform parent, string wheelName)
        {
            Transform t = parent.Find(wheelName);
            return t != null ? t.GetComponent<WheelCollider>() : null;
        }

        // ------------------------------------------------------------------
        // On duvar: kadro
        // ------------------------------------------------------------------

        /// <summary>
        /// Bisikleti tanidan govde. Uc noktadan boru cizilir; boylece
        /// "cadde borusu" gorseli, dikdortgen yigin olmaktan iyi okunur.
        /// </summary>
        private static void BuildFrame(Transform parent, Material frameMat, Material tireMat)
        {
            Vector3 bottomBracket = new Vector3(0f, 0.40f, -0.05f);
            Vector3 seatTop = new Vector3(0f, 1.02f, -0.32f);
            Vector3 headBottom = new Vector3(0f, 0.74f, 0.66f);
            Vector3 headTop = new Vector3(0f, 1.08f, 0.58f);
            Vector3 frontAxle = new Vector3(0f, DefWheelRadius, FrontAxleZ);

            // Ana borular
            CreateTube("Tube_Seat", bottomBracket, seatTop, 0.055f, parent, frameMat);
            CreateTube("Tube_Down", bottomBracket, headBottom, 0.055f, parent, frameMat);
            CreateTube("Tube_Top", seatTop, headTop, 0.055f, parent, frameMat);
            CreateTube("Tube_Head", headBottom, headTop, 0.07f, parent, frameMat);

            // Catalk (iki bacak)
            CreateTube("Fork_L", new Vector3(-0.06f, 0.74f, 0.66f), frontAxle,
                0.045f, parent, frameMat);
            CreateTube("Fork_R", new Vector3(0.06f, 0.74f, 0.66f), frontAxle,
                0.045f, parent, frameMat);

            // Zincir kolu ve kadro kolu: arka tekerlere baglanir.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.19f;
                CreateTube("ChainStay" + Side(side), bottomBracket,
                    new Vector3(x, DefWheelRadius, RearAxleZ), 0.04f, parent, frameMat);
                CreateTube("SeatStay" + Side(side), seatTop,
                    new Vector3(x, DefWheelRadius, RearAxleZ), 0.04f, parent, frameMat);
            }

            // Gidon
            CreateTube("Stem", headTop, new Vector3(0f, 1.12f, 0.68f), 0.05f, parent, frameMat);
            CreateTube("HandleBar", new Vector3(-0.30f, 1.12f, 0.68f),
                new Vector3(0.30f, 1.12f, 0.68f), 0.05f, parent, frameMat);

            // Tutamalar (lastik renginde: elin gidecegi yer)
            CreateBox("Grip_L", new Vector3(-0.27f, 1.12f, 0.68f),
                new Vector3(0.12f, 0.07f, 0.07f), parent, tireMat);
            CreateBox("Grip_R", new Vector3(0.27f, 1.12f, 0.68f),
                new Vector3(0.12f, 0.07f, 0.07f), parent, tireMat);

            // Sele. Kasanin on duvarina degmesin diye birkac cm onde
            // tutulur (kasa on yuzu z = -0.375).
            CreateTube("SeatPost", seatTop, new Vector3(0f, 1.12f, -0.22f), 0.045f, parent, frameMat);
            CreateBox("Seat", new Vector3(0f, 1.16f, -0.16f),
                new Vector3(0.16f, 0.06f, 0.28f), parent, tireMat);

            // Pedal kranksi: orta borunun ucunda ince bir disk.
            CreateCylinder("Crank", bottomBracket, new Vector3(0.28f, 0.03f, 0.28f),
                Quaternion.Euler(0f, 0f, 90f), parent, tireMat);

            // Govde alti guverte: sert platformun gorsel karsiligi. Ust
            // yuzeyi kasa tabaniyla ayni seviyede biter.
            CreateBox("Deck", new Vector3(0f, 1.065f, -0.35f),
                new Vector3(0.80f, 0.05f, 2.20f), parent, frameMat);
        }

        private static string Side(int side) => side < 0 ? "_L" : "_R";

        // ------------------------------------------------------------------
        // Arka kasa
        // ------------------------------------------------------------------

        /// <summary>
        /// Kasa: acik kutuyu andiran cerceve (taban + 4 kenar duvari).
        /// Icinde BOS bir cocuk ("CargoBed") birakilir; kutular
        /// <see cref="CargoBikeVisual"/> tarafindan buraya dizilir.
        ///
        /// Duvarlar collider'sizdir: kurye kasaya girip kutuyu alabilmeli,
        /// kutular da aracin fizigini elle degistirmemeli. Kasa tabani
        /// kokteki BoxCollider'in ust yuzeyinde biter.
        /// </summary>
        private static Transform BuildCargoBed(Transform parent, Material bedMat)
        {
            var cage = new GameObject("CargoCage");
            cage.transform.SetParent(parent, false);

            float outerX = BedInnerX + BedWall * 2f;
            float outerZ = BedInnerZ + BedWall * 2f;
            float wallCenterY = BedBaseY + BedBaseThickness + BedWallHeight * 0.5f;

            // Taban
            CreateBox("BedBase", new Vector3(0f, BedBaseY + BedBaseThickness * 0.5f, BedCenterZ),
                new Vector3(outerX, BedBaseThickness, outerZ), cage.transform, bedMat);

            // On / arka duvarlar (yanlar arasinda kalir)
            CreateBox("BedWall_Front",
                new Vector3(0f, wallCenterY, BedCenterZ + BedInnerZ * 0.5f + BedWall * 0.5f),
                new Vector3(BedInnerX, BedWallHeight, BedWall), cage.transform, bedMat);
            CreateBox("BedWall_Back",
                new Vector3(0f, wallCenterY, BedCenterZ - BedInnerZ * 0.5f - BedWall * 0.5f),
                new Vector3(BedInnerX, BedWallHeight, BedWall), cage.transform, bedMat);

            // Yan duvarlar koseleri de kapatir
            CreateBox("BedWall_Left",
                new Vector3(-(BedInnerX * 0.5f + BedWall * 0.5f), wallCenterY, BedCenterZ),
                new Vector3(BedWall, BedWallHeight, outerZ), cage.transform, bedMat);
            CreateBox("BedWall_Right",
                new Vector3(BedInnerX * 0.5f + BedWall * 0.5f, wallCenterY, BedCenterZ),
                new Vector3(BedWall, BedWallHeight, outerZ), cage.transform, bedMat);

            // Destek direkleri, capraz payandalar ve ust ray: kasa havada
            // durmasin. Direkler arka tekerlerin DISINDA (x = +-0.505),
            // tekerler +-0.20'de; gorsel olarak kesismezler.
            float deckY = BedBaseY;
            float postX = BedInnerX * 0.5f + BedWall * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * postX;
                CreateTube("BedPost" + Side(side),
                    new Vector3(x, 0.36f, BedCenterZ), new Vector3(x, deckY, BedCenterZ),
                    0.05f, parent, bedMat);
                CreateTube("BedBrace" + Side(side),
                    new Vector3(0f, 0.40f, -0.05f), new Vector3(x, deckY, BedCenterZ),
                    0.045f, parent, bedMat);
            }
            CreateTube("BedRail", new Vector3(-postX, deckY, BedCenterZ),
                new Vector3(postX, deckY, BedCenterZ), 0.05f, parent, bedMat);

            // BOS cocuk: kutularin oturacagi zemin. Kasanin ICINDE ve
            // tabanin ust yuzeyinde.
            var bed = new GameObject("CargoBed");
            bed.transform.SetParent(cage.transform, false);
            bed.transform.localPosition = new Vector3(0f, BedBaseY + BedBaseThickness, BedCenterZ);

            return bed.transform;
        }

        // ------------------------------------------------------------------
        // Kargo noktasi
        // ------------------------------------------------------------------

        /// <summary>
        /// Bisikletin kargo kapisi. Van noktasiyla ayni olcu yaklasimi:
        /// kok cocuk nesne + isTrigger BoxCollider + VanCargoPoint.
        ///
        /// Configure CAGIRILMAZ: imzasi IVehicleController ve sistem
        /// referanslari aliyor; CompositionRoot bu isi yapar.
        /// </summary>
        /// <summary>
        /// Bisikletin surucu kamerasini kurar.
        ///
        /// Neden ayri kamera: her aracin bir surucusu vardir ve ayni anda
        /// ikisi birden kullanilamaz. Kurye arac degistirirken kamerayı da
        /// degistirir (CourierController.SetActiveVehicle).
        ///
        /// DIKKAT: MainCamera TAGI VERILMEZ. "MainCamera" TEK olmalidir
        /// (Camera.main o tag'i arar); tag'i bisiklete verirsek van
        /// kamerasi Camera.main olmaktan cikar ve oyun kamerasiz kalir.
        /// </summary>
        private static void BuildDriverCamera(Transform bike)
        {
            var camGo = new GameObject("BikeCamera");
            camGo.transform.SetParent(bike, false);

            // Gidonun hemen arkasinda, kafanin hizinda.
            camGo.transform.localPosition = new Vector3(0f, 1.25f, 0.35f);
            camGo.transform.localRotation = Quaternion.identity;

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.48f, 0.60f);
            cam.fieldOfView = 70f;
            // Bisiklet kamerasi gidonun cok onunde: fazla yakinlik
            // kadroya kirpilir.
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 400f;

            var follow = camGo.AddComponent<KargoSim.Player.CameraFollow>();
            follow.ConfigureForSetup(
                true,                                             // surucu modu
                new Vector3(0f, 1.25f, 0.35f),                   // surucu offseti
                bike);
        }

        private static void BuildCargoPoint(Transform parent)
        {
            var point = new GameObject("VanCargoPoint");
            point.transform.SetParent(parent, false);
            // Kasanin uzerinde: oyuncu kutu almak icin bisikletin arkasina
            // yurur.
            point.transform.localPosition = new Vector3(0f, BedBaseY + 0.55f, BedCenterZ);

            var box = point.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.8f, 1.8f, 1.8f);

            point.AddComponent<VanCargoPoint>();
        }

        // ------------------------------------------------------------------
        // Sahneye yerlestirme
        // ------------------------------------------------------------------

        private static void PlaceInScene(StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[KargoBisiklet] Once prefabi kurun: " + PrefabPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Menu tekrar calistirilabilir olmali: eski arac temizlenir.
            RemoveExisting(scene);

            // Depo, mevcut aracin park ettigi yerde. Bisiklet 3-4 m
            // yaninda ve FARKLI ACIyla durur: oyuncu iki araci da bulur,
            // ikisi de ayni anda gorunur.
            Vector3 depot = DepotPosition(scene);
            Vector3 place = depot + new Vector3(3.6f, 0.6f, 1.2f);
            Quaternion facing = Quaternion.Euler(0f, -40f, 0f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = place;
            instance.transform.rotation = facing;
            Undo.RegisterCreatedObjectUndo(instance, "CargoBike");

            // BASLANGIÇ ARACI (Asama 14): CompositionRoot'un playerVehicle
            // alanini bisiklet tanimina cevir. Bu alan kargo KAPASITESINI
            // belirler; van tanimi kalirsa oyuncu bisiklete bindiginde HUD
            // "350 kg bos" der ve bisikletin 60 kg sinifi uygulanmaz.
            var root = UnityEngine.Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            if (root != null)
            {
                var bikeDef = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(VehicleDefPath);
                if (bikeDef != null)
                {
                    root.SetPlayerVehicleForSetup(bikeDef);
                    EditorUtility.SetDirty(root);
                }
                else
                {
                    Debug.LogWarning("[KargoBisiklet] Bisiklet tanimi bulunamadi: " + VehicleDefPath);
                }
            }
            else
            {
                Debug.LogWarning("[KargoBisiklet] GameCompositionRoot bulunamadi; " +
                                 "playerVehicle elle atanmalı.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            float distance = Vector3.Distance(new Vector3(place.x, 0f, place.z), depot);
            report.AppendLine("  prefab      : " + PrefabPath);
            report.AppendLine("  sahne konumu: " + place.ToString("0.0")
                              + "  aci " + facing.eulerAngles.y.ToString("0") + " derece");
            report.AppendLine("  depo mesafe : " + distance.ToString("0.0") + " m");
            report.AppendLine("  baslangic   : EVET (playerVehicle = bisiklet tanimi)");
            report.AppendLine("  kamera      : surucu modunda, prefab icinde");
        }

        /// <summary>Sahnedeki eski bisikleti siler (yoksa hicbir sey olmaz).</summary>
        private static void RemoveExisting(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var bikes = roots[i].GetComponentsInChildren<BicycleController>(true);
                for (int b = 0; b < bikes.Length; b++)
                    if (bikes[b] != null) UnityEngine.Object.DestroyImmediate(bikes[b].gameObject);
            }
        }

        /// <summary>
        /// Depo konumu. MapLayout atanmissa oradan, yoksa mevcut aracin
        /// konumundan okunur. Ikisi de yoksa sifir.
        /// </summary>
        private static Vector3 DepotPosition(Scene scene)
        {
            var root = FindInScene<GameCompositionRoot>(scene);
            if (root != null)
            {
                // mapLayout alani private: SerializedObject ile okunur.
                var so = new SerializedObject(root);
                SerializedProperty layout = so.FindProperty("mapLayout");
                var map = layout != null ? layout.objectReferenceValue as MapLayout : null;
                if (map != null) return map.depotPosition;
            }

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var van = roots[i].GetComponentInChildren<ArcadeVehicleController>(true);
                if (van != null) return van.transform.position;
            }

            return Vector3.zero;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Dogrulama
        // ------------------------------------------------------------------

        /// <summary>
        /// Kaydedilmis prefabi diskten okuyup baglantilari dogrular.
        /// Uretim sessizce yarim kalmis olabilir (ornegin teker referansi
        /// null) ve oyuncu suruse basunca hicbir sey olmazdi.
        /// </summary>
        private static void VerifyPrefab(StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[KargoBisiklet] Dogrulama: prefab bulunamadi " + PrefabPath);
                return;
            }

            var problems = new StringBuilder();

            var controller = prefab.GetComponent<BicycleController>();
            if (controller == null) problems.Append(" BicycleController yok.");
            else if (controller.Definition == null) problems.Append(" VehicleDefinition atanmamis.");

            var rb = prefab.GetComponent<Rigidbody>();
            if (rb == null) problems.Append(" Rigidbody yok.");

            // GOVDE COLLIDER'I ZORUNLU: sadece tekerlerden olan arac
            // ilk itki kuvvetiyle takla atar.
            var chassis = prefab.GetComponent<BoxCollider>();
            if (chassis == null) problems.Append(" Sasi BoxCollider'i yok (takla atar).");

            var wheels = prefab.GetComponentsInChildren<WheelCollider>(true);
            if (wheels.Length != 3) problems.Append(" Teker sayisi " + wheels.Length + ", 3 olmali.");

            var wheelVisuals = prefab.GetComponentsInChildren<WheelVisual>(true);
            if (wheelVisuals.Length != 3)
                problems.Append(" Teker gorseli " + wheelVisuals.Length + ", 3 olmali.");

            var visual = prefab.GetComponent<CargoBikeVisual>();
            if (visual == null) problems.Append(" CargoBikeVisual yok.");
            else if (visual.CargoBed == null) problems.Append(" CargoBed atanmamis.");

            var point = prefab.GetComponentInChildren<VanCargoPoint>(true);
            if (point == null) problems.Append(" VanCargoPoint yok.");
            else if (point.GetComponent<BoxCollider>() == null)
                problems.Append(" Kargo noktasi collider'i yok.");

            if (problems.Length == 0)
            {
                report.AppendLine("  dogrulama   : OK (3 teker, kasa, kargo noktasi bagli)");
                Debug.Log("[KargoBisiklet] Dogrulama OK: 3 tekerli, kasali, tanim bagli.");
            }
            else
            {
                Debug.LogError("[KargoBisiklet] Dogrulama BASARISIZ:" + problems);
            }
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        //
        // BASKA DOSYALARIN YARDIMCILARI (orn. ShopSetupTool.CreateBox)
        // KULLANILMAZ: baska bir editor dosyasindaki yardimciya baglanmak,
        // o dosya yeniden yazildiginda bu araci da kirmaya yol acar.
        // Burada kendi primitive kodumuz var.
        // ------------------------------------------------------------------

        private static GameObject CreateBox(string boxName, Vector3 localPosition,
                                           Vector3 size, Transform parent, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;

            // Bu parcalar YALNIZCA gorseldir. Sert govde collider'i kok
            // nesnedeki BoxCollider'dir; burada fazladan collider her yanda
            // hem fizik maliyeti hem de kuryenin aracin altina girip
            // surus bozulmasi sorununa yol acar.
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());

            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            box.transform.localRotation = Quaternion.identity;
            ApplyMaterial(box, material);
            return box;
        }

        private static GameObject CreateCylinder(string cylinderName, Vector3 localPosition,
                                                 Vector3 size, Quaternion localRotation,
                                                 Transform parent, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = cylinderName;
            UnityEngine.Object.DestroyImmediate(cylinder.GetComponent<Collider>());

            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localScale = size;
            cylinder.transform.localRotation = localRotation;
            ApplyMaterial(cylinder, material);
            return cylinder;
        }

        /// <summary>
        /// Iki nokta arasinda ince dikdortgen boru. Uzamis primitive,
        /// kadroyu okunur kilar.
        /// </summary>
        private static GameObject CreateTube(string tubeName, Vector3 from, Vector3 to,
                                             float thickness, Transform parent, Material material)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.0001f) length = 0.0001f;

            GameObject tube = CreateBox(tubeName, (from + to) * 0.5f,
                new Vector3(thickness, length, thickness), parent, material);

            // Unity yerel +Y ekseni boyunca uzanir; boruyu from -> to
            // yonune cevir.
            tube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            return tube;
        }

        private static void ApplyMaterial(GameObject go, Material material)
        {
            if (material == null) return;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = material;
        }

        private static Material CreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // URP'de standart shader "Universal Render Pipeline/Lit".
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[KargoBisiklet] URP/Lit shader bulunamadi. "
                               + "Render pipeline URP degil mi?");
                return null;
            }

            var material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path),
                color = color
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void AssignPrivateField(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[KargoBisiklet] Alan bulunamadi: " + fieldName);
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                PrefabFolder,
                "Assets/Art/Materials",
                "Assets/ScriptableObjects/Definitions",
            };
            foreach (string f in folders)
            {
                if (AssetDatabase.IsValidFolder(f)) continue;

                string[] parts = f.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }
    }
}
