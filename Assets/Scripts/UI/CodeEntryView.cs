using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Player;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// Adres kapisinda acilan TESLIMAT KODU paneli.
    ///
    /// AKIS:
    ///   E -> <see cref="AddressDoor"/> kapiyi calar -> NPC konusur ->
    ///   <c>GameEvents.NPCAnswered</c> yayinlanir -> bu panel acilir ->
    ///   oyuncu rakam yazar -> Enter -> <see cref="AddressDoor.SubmitCode"/>.
    ///
    /// BU SINIF Update() YAZMAZ. Proje kurali "oyunda tek Update"
    /// (<c>GameCompositionRoot.Update</c>). Girdi <see cref="Tick"/> ile
    /// okunur.
    ///   // TODO(agent): UIManager.Tick bu metodu cagirmali
    ///   codeEntry?.Tick(deltaTime);
    ///
    /// MVP KURALI - KOD EKRANA YAZILMAZ:
    /// NPC "kodu soyluyor" ama oyuncu yalnizca YAZABILIR. Dogru kod
    /// ekrana cikarsa dogrulamanin anlami kalir; bu yuzden
    /// <see cref="OnNPCAnswered"/> icindeki <c>code</c> parametresi
    /// BILINCLI OLARAK kullanilmaz ve sinifta hicbir yerde saklanmaz.
    /// Ekrandaki tek rakam kaynagi oyuncunun kendi tus basinidir.
    ///
    /// YAZI DILINDE DIYAKRITIK YOK: font LegacyRuntime, Turkce karakter
    /// (k, s, i, g, u, o, c) icermiyor. Projedeki tum arayuz metinleri
    /// ASCII yazili; burada da ayni kural.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CodeEntryView : MonoBehaviour
    {
        /// <summary>Kod hane sayisi (DeliveryCode ile ayni kaynak).</summary>
        private const int MaxDigits = DeliveryCode.Digits;

        /// <summary>Basari / uyari sonrasi panelin kendini kapattigi sure.</summary>
        private const float SuccessCloseDelay = 0.9f;
        private const float NoticeCloseDelay = 1.2f;

        private const string FallbackNpcName = "Musteri";
        private const string HintIdle = "Rakam yaz, ENTER ile onayla, ESC iptal";
        private const string HintSessionClosed =
            "Kapi oturumu kapandi - kapiyi tekrar cal (E)";

        [Header("Oturum")]
        [Tooltip("Konusma baslayan kapi. CompositionRoot tarafindan Configure ile verilir.")]
        [SerializeField] private AddressDoor door;

        [Header("Parcalar")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text npcLineText;
        [SerializeField] private Text inputText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text hintText;
        [SerializeField] private Button confirmButton;

        private string _entry = string.Empty;
        private readonly System.Text.StringBuilder _entryBuffer = new System.Text.StringBuilder(16);

        /// <summary>
        /// Otomatik kapanma sayaci. -1 = sayac yok (panel acik kalir).
        /// </summary>
        private float _closeDelay = -1f;

        private bool _subscribed;
        private bool _warnedMissingPanel;
        private bool _warnedMissingDoor;

        /// <summary>Panel gorunur mu?</summary>
        public bool IsOpen { get; private set; }

        // ------------------------------------------------------------------
        // Baglama
        // ------------------------------------------------------------------

        /// <summary>
        /// Kapidan baglanir ve <c>GameEvents</c> dinlemelerini kurar.
        /// CompositionRoot cagirir; birden fazla cagrilabilir (eski
        /// abonelik once birakilir).
        /// </summary>
        public void Configure(AddressDoor value)
        {
            door = value;
            _warnedMissingDoor = false;

            Unsubscribe();
            Subscribe();
        }

        private void Awake()
        {
            // Buton dinleyicisi. Mevcut desen (ParcelRowView) ile ayni.
            // NOT: kok GameObject kurulumda KAPALI birakildigi icin bu
            // metod ancak panel ilk kez ACTIVATE edildiginde calisir; yani
            // gorunur buton her zaman dinleyicisiz kalmaz.
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        }

        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            GameEvents.NPCAnswered += OnNPCAnswered;
            GameEvents.DeliveryCodeSubmitted += OnSubmitted;
            GameEvents.CustomerNotHome += OnCustomerNotHome;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            GameEvents.NPCAnswered -= OnNPCAnswered;
            GameEvents.DeliveryCodeSubmitted -= OnSubmitted;
            GameEvents.CustomerNotHome -= OnCustomerNotHome;
        }

        // ------------------------------------------------------------------
        // Panel acma / kapama
        // ------------------------------------------------------------------

        public void Open()
        {
            if (!EnsurePanel()) return;

            IsOpen = true;
            _closeDelay = -1f;
            SetVisible(true);
            RefreshEntry();
        }

        public void Close()
        {
            IsOpen = false;
            _closeDelay = -1f;
            _entry = string.Empty;
            SetVisible(false);
        }

        /// <summary>Oyuncu vazgecildi: kapi oturumu da iptal edilir.</summary>
        public void Cancel()
        {
            // door yoksa sessizce yalnizca panel kapanir.
            // DIKKAT: `door?.` YAZILMAZ. Unity'da "yok edilmis" nesne de
            // null DEGILDIR; `?.` Unity'nin overload'i olan == operatorunu
            // atlayip MissingReferenceException firlatir.
            if (door != null) door.CancelCodeEntry();
            Close();
        }

        /// <summary>Girilen kodu dogrulatir (Enter veya Onayla butonu).</summary>
        public void Confirm()
        {
            // Baglama yoksa HICBIR sey yapma: oyuncu yanlislikla tuslarsa
            // kapiya istek atilmasin.
            if (door == null)
            {
                WarnMissingDoorOnce();
                return;
            }

            // AddressDoor YAN LIS kodda oturumu KAPATIR. Panel acik kalir
            // ama kapi artik kod beklemiyor; oyuncu tekrar Enter'a basinca
            // sessizce hicbir sey olmasin diye acikca yaziyoruz.
            if (door.IsAwaitingCode == false)
            {
                ShowMessage("Kapi kod beklemiyor. Kapiyi tekrar cal (E).", UITheme.Warning);
                if (hintText != null) hintText.text = HintSessionClosed;
                return;
            }

            // DeliveryCode.Verify yarim/"bos" kodu YANLIS sayar ve bu bir
            // deneme hakki yemesi demek. Oyuncu 4 haneyi yazmadan onaylarsa
            // hakkini yanlis harcamamasi icin burada durdurulur.
            if (_entry.Length < MaxDigits)
            {
                ShowMessage("Kod " + MaxDigits + " haneli olmali.", UITheme.Accent);
                return;
            }

            // Dogrulama AddressDoor'da; sonucu OnSubmitted dinler.
            door.SubmitCode(_entry);
        }

        // ------------------------------------------------------------------
        // Girdi (Update degil)
        // ------------------------------------------------------------------

        /// <summary>
        /// Her kare cagrilir (UIManager.Tick uzerinden). Panel kapaliyken
        /// HICBIR tus okunmaz.
        /// </summary>
        public void Tick(float deltaTime)
        {
            SyncOpenFromHierarchy();
            if (IsOpen == false) return;

            // Otomatik kapanma sayaci (dogru kod / musteri evde degil).
            if (_closeDelay > 0f)
            {
                _closeDelay -= deltaTime;
                if (_closeDelay <= 0f) { _closeDelay = -1f; Close(); }
                return;
            }

            // Klavye yalnizca OYNANIRKEN okunur. EditMode testleri
            // Input cagriyla degil, panel durumuyla ilgilenir; editor'de
            // Input cagrisi hata verip testi kirmizi yazardi.
            if (Application.isPlaying == false) return;

            ReadKeys();
        }

        private void ReadKeys()
        {
            // Rakamlar: hem ust satir (Alpha) hem sayi tus takimi (Keypad).
            // KODun KENDISI burada uretilmez; sadece oyuncunun bastigi tus.
            for (int digit = 0; digit <= 9; digit++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha0 + digit) &&
                    !Input.GetKeyDown(KeyCode.Keypad0 + digit)) continue;

                if (_entry.Length < MaxDigits) _entry += (char)('0' + digit);
                RefreshEntry();
                return; // tek basma = tek rakam
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Confirm();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (_entry.Length > 0) _entry = _entry.Substring(0, _entry.Length - 1);
                RefreshEntry();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        // ------------------------------------------------------------------
        // GameEvents tepkileri
        // ------------------------------------------------------------------

        private void OnNPCAnswered(int destinationId, string npcName, int code)
        {
            // KOD ARTIK GIZLENMIYOR (Asama 13 duzeltmesi).
            //
            // Onceki kurguda kod ekrana hic yazilmazdi ve ses de yoktu:
            // oyuncu 0000-9999 arasinda tahmin ediyordu. Bu bir teslimat
            // kodu degil, bilgisayar kiriciydi.
            //
            // Simdi NPC kodu hem SESLE (AudioDirector perde olarak soyler)
            // hem de NPC satirinda YAZILI olarak verir. Yazili gosterim
            // oyuncunun 4 haneyi ezberleme yukunu kaldirir; ses ise
            // "biri bana bir kod soyledi" hissini verir.
            //
            // DIKKAT: gosterilen kod gecici bir satirdir. GIRIS kutulari
            // (_entry) yine de oyuncunun kendi basinindan dolar; yani
            // oyuncu yalnizca gorup doldurur, otomatik doldurulmaz.
            Open();

            if (titleText != null) titleText.text = "TESLIMAT KODU";

            if (npcLineText != null)
                npcLineText.text = (string.IsNullOrEmpty(npcName) ? FallbackNpcName : npcName)
                                   + " size bir kod soyledi: " + DeliveryCode.Format(code);

            _entry = string.Empty;
            ShowMessage(string.Empty, UITheme.TextPrimary);
            if (hintText != null) hintText.text = HintIdle;
            RefreshEntry();
        }

        private void OnSubmitted(Parcel parcel, string input, bool correct)
        {
            if (correct)
            {
                ShowMessage("Teslimat onaylandi.", UITheme.Success);
                _closeDelay = SuccessCloseDelay;
                return;
            }

            ShowMessage("Teslimat kodu yanlis.", UITheme.Danger);

            // Panel ACIK kalir: oyuncu tekrar denesin.
            _entry = string.Empty;
            RefreshEntry();

            // ...ama AddressDoor yanlis kodda oturumu kapatti; ipucu bunu
            // saklamasin, yoksa oyuncu "Enter calismiyor" sanir.
            if (hintText != null) hintText.text = HintSessionClosed;
        }

        private void OnCustomerNotHome(int destinationId)
        {
            // Bu olay kod ekrani ACILMADAN gelir (kapiyi calmak -> musteri
            // yok). Mesajin gorunebilmesi icin panel kisa sureligine
            // acilip kendini kapatir.
            if (IsOpen == false) Open();

            if (titleText != null) titleText.text = "KAPI CALINDI";
            if (npcLineText != null) npcLineText.text = "Kurye kapiyi caldi.";
            if (hintText != null) hintText.text = "Kargo elde kaldi. Adrese tekrar gel.";

            _entry = string.Empty;
            ShowMessage("Musteri evde degil.", UITheme.Warning);
            RefreshEntry();

            _closeDelay = NoticeCloseDelay;
        }

        // ------------------------------------------------------------------
        // Gorunum
        // ------------------------------------------------------------------

        /// <summary>
        /// "[ 1 _ _ _ ]" bicimi: girilen rakamlar + bos yerler alt cizgi.
        /// </summary>
        private void RefreshEntry()
        {
            if (inputText == null) return;

            _entryBuffer.Length = 0;
            _entryBuffer.Append("[ ");
            for (int slot = 0; slot < MaxDigits; slot++)
            {
                if (slot > 0) _entryBuffer.Append(' ');
                _entryBuffer.Append(slot < _entry.Length ? _entry[slot] : '_');
            }
            _entryBuffer.Append(" ]");

            inputText.text = _entryBuffer.ToString();
        }

        private void ShowMessage(string message, Color color)
        {
            if (messageText == null) return;
            messageText.text = message;
            messageText.color = color;
        }

        /// <summary>
        /// Panel referansini garanti eder. Kurulum menusi calistirilmamissa
        /// oyun kilitli kalmasin diye cocuk "Panel" adiyla bulunur
        /// (DepotView / StopView ile ayni neden).
        /// </summary>
        private bool EnsurePanel()
        {
            if (panelRoot != null) return true;

            Transform child = transform.Find("Panel");
            if (child == null)
            {
                if (!_warnedMissingPanel)
                {
                    _warnedMissingPanel = true;
                    Debug.LogError("[CodeEntryView] Panel bulunamadi. " +
                                   "Menu: Kargo/6. Arayuzu Kur", this);
                }
                return false;
            }

            panelRoot = child as RectTransform;
            Debug.LogWarning("[CodeEntryView] panelRoot referansi BOS oldu; 'Panel' cocuguyla " +
                             "duzeltildi. Kalici cozum: Kargo/6. Arayuzu Kur", this);
            return panelRoot != null;
        }

        /// <summary>
        /// Paneli gosterir/gizler.
        ///
        /// DIKKAT: kurulum araci kok GameObject'i KAPALI birakir (sahne
        /// temiz olsun diye). Kok kapaliyken cocuk SetActive(true) HIC BIR
        /// SEY GOSTERMEZ; bu yuzden once kok acarilir. Bkz. BuildDepot
        /// icindeki ayni uyari.
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (OwnsPanelRoot() && gameObject.activeSelf != visible)
                gameObject.SetActive(visible);

            if (panelRoot != null) panelRoot.gameObject.SetActive(visible);
        }

        /// <summary>Bu view, panelin kokunde (ya da kokunun kendisinde) mi?</summary>
        private bool OwnsPanelRoot()
        {
            if (panelRoot == null) return false;
            return panelRoot == transform || panelRoot.IsChildOf(transform);
        }

        /// <summary>
        /// Panel baska bir yerden kapatildiysa IsOpen gercek duruma
        /// guncellenir. Aksi halde gorunmeyen panel tus okumaya devam eder.
        /// </summary>
        private void SyncOpenFromHierarchy()
        {
            if (panelRoot == null) return;
            IsOpen = panelRoot.gameObject.activeSelf;
        }

        private void WarnMissingDoorOnce()
        {
            if (_warnedMissingDoor) return;
            _warnedMissingDoor = true;
            Debug.LogWarning("[CodeEntryView] AddressDoor baglanmadi. " +
                             "CompositionRoot -> codeEntry.Configure(door) cagrilmali.", this);
        }
    }
}
