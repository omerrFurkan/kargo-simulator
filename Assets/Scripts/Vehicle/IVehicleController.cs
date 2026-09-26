using UnityEngine;

namespace KargoSim.Vehicle
{
    /// <summary>
    /// Her arac tipinin uymasi gereken ASGARI sozlesme.
    ///
    /// NEDEN VAR: proje basinda tek arac vardi (ArcadeVehicleController)
    /// ve kargo noktasi, kurye, composition root hepsi O SINIFA dogrudan
    /// bagliydi. Ikinci bir arac (kargo bisikleti) eklenince bu baglilik
    /// ikinci araci da ayni fizik motoruyla surmek zorunda birakardi -
    /// oysa bisikletin direksiyonu ve kurgusu farkli.
    ///
    /// Bu arayuz ikisini ayirir: kargo noktasi "arac hizlidir" ve "arac
    /// nerededir" sorularini sorar, fizigin NASIL oldugunu bilmez.
    ///
    /// GERIYE UYUMLULUK: mevcut <see cref="ArcadeVehicleController"/> bu
    /// arayuzu UYGULAR; imzalar degismedi. Onu kullanan cagrilar
    /// (CompositionRoot, testler) oldugu gibi calisir.
    /// </summary>
    public interface IVehicleController
    {
        /// <summary>Anlik hiz (km/sa).</summary>
        float SpeedKmh { get; }

        /// <summary>
        /// Oyuncu kontrol edemiyor mu? Binilmedikte araç kilitlidir:
        /// yoldan geçse bile oyuncu onu surmemisdir.
        /// </summary>
        bool ControlsLocked { get; set; }

        /// <summary>Aracin kok donusumu (cıkış noktası hesabı için).</summary>
        Transform VehicleTransform { get; }

        /// <summary>
        /// Arac uzerinde kullanilacak kamerayi baglar. Binildiginde
        /// kamera buraya gecer.
        /// </summary>
        void SetDriverCamera(Camera camera);

        /// <summary>Araci baslangic konumuna dondurur (gun sonu, ters dunya).</summary>
        void Respawn();
    }
}
