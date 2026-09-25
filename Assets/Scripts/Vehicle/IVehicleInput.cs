namespace KargoSim.Vehicle
{
    /// <summary>
    /// Aracı neyin surdugunun sozlesmesi. ArcadeVehicleController sadece bu
    /// arayuzu bilir; klavye, gamepad ya da yapay zeka fark etmez.
    ///
    /// Boylece surus sistemi oyuncu girdisine baglanmaz ve test edilebilir.
    ///
    /// DIKKAT: Bu arayuz Vehicle katmaninda durur ve Player'a BILMEZ.
    /// Aracin "cevirme tusu" gibi bir oyuncu aksiyonuna ihtiyaci olmasi
    /// halinde bile, bunu arayuz uzerinden ister; Player'a baglanmaz.
    /// </summary>
    public interface IVehicleInput
    {
        /// <summary>-1 (geri) .. +1 (ileri). 0 = bos.</summary>
        float Throttle { get; }

        /// <summary>-1 (sol) .. +1 (sag). 0 = duz.</summary>
        float Steer { get; }

        /// <summary>El freni basili mi?</summary>
        bool Handbrake { get; }

        /// <summary>Surucu araci mevcut durumundan kurtarmak istiyor (R tusu).</summary>
        event System.Action RespawnRequested;
    }
}
