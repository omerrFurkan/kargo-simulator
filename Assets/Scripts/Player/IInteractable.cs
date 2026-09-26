using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// E'ye basildiginda tepki veren her sey bu sozlesmeyi uygular.
    ///
    /// Neden arayuz: once "E = yukle" seklinde kodda dagilmis bir
    /// kural vardi; simdi oyunun birden fazla etkilesim noktasi var
    /// (dukkan rafi, arac on kismi, arac arka kismi, adres kapisi).
    /// Her biri kendi ipucunu ve kendi davranisini bildirir; oyuncu
    /// "E"ye basinca ne olacagini gormek zorunda kalmaz, cunku ne
    /// olacagini E'nin YANINDA yazar.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Ekran ipucunda gosterilecek metin (orn. "E: Kargo al").</summary>
        string Prompt { get; }

        /// <summary>Bu an etkilesime girilebilir mi?</summary>
        bool CanInteract(CourierController courier);

        /// <summary>E'ye basildi.</summary>
        void Interact(CourierController courier);
    }
}
