using DataAnonymizer.Services;

namespace DataAnonymizer;

/// <summary>Alle Oberflächen-Texte einer Sprache.</summary>
public sealed class UiText
{
    public required string PageTitle { get; init; }
    public required string AppTitle { get; init; }
    public required string Tagline { get; init; }
    public required string PrivacyBadge { get; init; }
    public required string HowTitle { get; init; }
    public required string Step1Title { get; init; }
    public required string Step1Desc { get; init; }
    public required string Step2Title { get; init; }
    public required string Step2Desc { get; init; }
    public required string Step3Title { get; init; }
    public required string Step3Desc { get; init; }
    public required string AdvancedTitle { get; init; }
    public required string AdvancedHint { get; init; }
    public required string DsgNote { get; init; }

    public required string CardOriginal { get; init; }
    public required string InputPlaceholder { get; init; }
    public required string BtnAnonymize { get; init; }
    public required string BtnAnonymizeBusy { get; init; }
    public required string BtnClear { get; init; }
    public required string BtnPaste { get; init; }
    public required string SummaryLine { get; init; }   // {0} = Werte, {1} = Kategorien

    public required string CardAnonymized { get; init; }
    public required string BtnCopy { get; init; }
    public required string Copied { get; init; }
    public required string MappingTitle { get; init; }        // {0} = Anzahl
    public required string ThPlaceholder { get; init; }
    public required string ThOriginal { get; init; }
    public required string ThCategory { get; init; }
    public required string MappingNote { get; init; }
    public required string NoPiiFound { get; init; }
    public required string SensitiveWarning { get; init; }   // {0} = Anzahl
    public required string MappingLoadedNote { get; init; }   // {0} = Anzahl
    public required string BtnExportMapping { get; init; }
    public required string BtnImportMapping { get; init; }
    public required string BtnDeleteMapping { get; init; }

    public required string CardDeanonymize { get; init; }
    public required string DeanonHint { get; init; }
    public required string ResponsePlaceholder { get; init; }
    public required string BtnDeanonymize { get; init; }
    public required string RestoredTitle { get; init; }

    public required string CardRules { get; init; }
    public required string OptNames { get; init; }
    public required string OptEmails { get; init; }
    public required string OptPhones { get; init; }
    public required string OptStreets { get; init; }
    public required string OptCities { get; init; }
    public required string OptIban { get; init; }
    public required string OptSsn { get; init; }
    public required string OptCards { get; init; }
    public required string OptRefs { get; init; }
    public required string OptPlates { get; init; }
    public required string OptOrgs { get; init; }
    public required string OptIp { get; init; }
    public required string OptSensitive { get; init; }
    public required string OptBirthdays { get; init; }
    public required string OptAllDates { get; init; }

    public required string CardTerms { get; init; }
    public required string TermsHint { get; init; }
    public required string TermsPlaceholder { get; init; }

    public required string CardAllowed { get; init; }
    public required string AllowedHint { get; init; }
    public required string AllowTooltip { get; init; }
    public required string AllowedChipsTitle { get; init; }
    public required string AllowedChipRemoveTooltip { get; init; }

    public required string CardLlm { get; init; }
    public required string LlmEnable { get; init; }
    public required string LlmHint { get; init; }
    public required string LlmStatusChecking { get; init; }
    public required string LlmStatusReady { get; init; }
    public required string LlmStatusOffline { get; init; }
    public required string LlmOfflineHelp { get; init; }
    public required string LlmModelLabel { get; init; }
    public required string LlmAnalyzing { get; init; }
    public required string LlmError { get; init; }
    public required string LlmStatusPulling { get; init; }    // {0} = Modell, {1} = Prozent
    public required string LlmPullNote { get; init; }
    public required string LlmPullFailed { get; init; }       // {0} = Modell
    public required string BtnRecheck { get; init; }
}

/// <summary>Hält die aktuelle Sprache einer Browser-Sitzung.</summary>
public sealed class LocalizationService
{
    public AppLanguage Language { get; private set; } = AppLanguage.De;
    public UiText T { get; private set; } = UiStrings.For(AppLanguage.De);

    public void Set(AppLanguage language)
    {
        Language = language;
        T = UiStrings.For(language);
    }

    public static readonly (AppLanguage Language, string Code, string NativeName)[] All =
    {
        (AppLanguage.De, "de", "Deutsch"),
        (AppLanguage.En, "en", "English"),
        (AppLanguage.Fr, "fr", "Français"),
        (AppLanguage.It, "it", "Italiano")
    };

    public static AppLanguage FromCode(string? code) => code switch
    {
        "en" => AppLanguage.En,
        "fr" => AppLanguage.Fr,
        "it" => AppLanguage.It,
        _ => AppLanguage.De
    };

    public static string ToCode(AppLanguage language) => language switch
    {
        AppLanguage.En => "en",
        AppLanguage.Fr => "fr",
        AppLanguage.It => "it",
        _ => "de"
    };
}

/// <summary>Die Übersetzungen für Deutsch, Englisch, Französisch und Italienisch.</summary>
public static class UiStrings
{
    public static UiText For(AppLanguage language) => language switch
    {
        AppLanguage.En => En,
        AppLanguage.Fr => Fr,
        AppLanguage.It => It,
        _ => De
    };

    private static readonly UiText De = new()
    {
        PageTitle = "Daten-Anonymisierer",
        AppTitle = "🔒 Daten-Anonymisierer",
        Tagline = "Ersetzt persönliche Daten durch Platzhalter, bevor du einen Fall in ein KI-Tool einfügst – und setzt die echten Daten danach wieder ein.",
        PrivacyBadge = "🔒 Alles bleibt auf diesem Computer – es werden keine Daten ins Internet gesendet.",
        HowTitle = "So funktioniert's",
        Step1Title = "1. Anonymisieren",
        Step1Desc = "Text mit persönlichen Daten einfügen. Die App ersetzt Namen, Adressen usw. durch Platzhalter wie [NAME_1].",
        Step2Title = "2. Im KI-Tool nutzen",
        Step2Desc = "Den anonymisierten Text in Claude, ChatGPT o.Ä. einfügen und die Antwort oder das SQL-Skript erstellen lassen.",
        Step3Title = "3. Zurückübersetzen",
        Step3Desc = "Die Antwort hier einfügen – die Platzhalter werden wieder durch deine echten Daten ersetzt.",
        AdvancedTitle = "⚙️ Erweiterte Einstellungen",
        AdvancedHint = "Standardmässig ist alles sinnvoll eingestellt. Hier kannst du festlegen, was erkannt wird, und eigene Begriffe hinzufügen.",
        DsgNote = "Unterstützt den datenschutzkonformen Umgang mit Personendaten nach dem revidierten Schweizer Datenschutzgesetz (revDSG) – inkl. besonders schützenswerter Daten. Die Erkennung ist eine Hilfe und ersetzt die eigene Kontrolle nicht; dies ist keine Rechtsberatung.",
        CardOriginal = "1️⃣ Originaltext mit persönlichen Daten",
        InputPlaceholder = "Falltext hier einfügen, z.B.:\nHerr Max Muster (geb. 12.03.1985), Bahnhofstrasse 12, 8001 Zürich, Tel. +41 79 123 45 67, max.muster@example.ch, meldet einen Schaden. Policen-Nr. P-2023/4711, IBAN CH93 0076 2011 6238 5295 7.",
        BtnAnonymize = "🔒 Anonymisieren",
        BtnAnonymizeBusy = "⏳ Analysiere …",
        BtnClear = "Leeren",
        BtnPaste = "📥 Einfügen",
        SummaryLine = "{0} Werte in {1} Kategorien ersetzt",
        CardAnonymized = "2️⃣ Anonymisierter Text – sicher für Claude Code",
        BtnCopy = "📋 Kopieren",
        Copied = "✔ Kopiert",
        MappingTitle = "Zuordnungstabelle ({0} Werte ersetzt)",
        ThPlaceholder = "Platzhalter",
        ThOriginal = "Originalwert",
        ThCategory = "Kategorie",
        MappingNote = "Die Tabelle wird lokal in diesem Browser gespeichert, bis du sie löschst – so kannst du Antworten oder Skripte auch später noch zurückübersetzen.",
        NoPiiFound = "Keine persönlichen Daten gefunden.",
        SensitiveWarning = "⚠️ Achtung: {0} besonders schützenswerte Angabe(n) erkannt und ersetzt (revDSG). Bitte besonders sorgfältig prüfen.",
        MappingLoadedNote = "Gespeicherte Zuordnung geladen ({0} Einträge) – Antwort oder Skript unten einfügen und zurückersetzen.",
        BtnExportMapping = "⬇ Zuordnung exportieren",
        BtnImportMapping = "⬆ Zuordnung importieren",
        BtnDeleteMapping = "🗑 Zuordnung löschen",
        CardDeanonymize = "3️⃣ Antwort de-anonymisieren (Platzhalter zurückersetzen)",
        DeanonHint = "Antwort von Claude Code hier einfügen – die Platzhalter werden wieder durch die Originalwerte ersetzt.",
        ResponsePlaceholder = "Antwort mit Platzhaltern wie [NAME_1] hier einfügen …",
        BtnDeanonymize = "🔓 Platzhalter zurückersetzen",
        RestoredTitle = "Wiederhergestellter Text",
        CardRules = "⚙️ Erkennungsregeln",
        OptNames = "Namen (nach Herr/Frau, „Name:“, …)",
        OptEmails = "E-Mail-Adressen",
        OptPhones = "Telefonnummern",
        OptStreets = "Strassen & Hausnummern",
        OptCities = "PLZ & Ortschaften",
        OptIban = "IBAN",
        OptSsn = "AHV-/Sozialversicherungsnummern",
        OptCards = "Kreditkartennummern",
        OptRefs = "Kunden-/Policen-/Fall-Nummern",
        OptPlates = "Autokennzeichen (CH)",
        OptOrgs = "Firmen & Organisationen (nur mit KI)",
        OptIp = "IP-Adressen",
        OptSensitive = "Besonders schützenswerte Daten (revDSG, nur mit KI)",
        OptBirthdays = "Geburtsdaten („geb.“, „geboren am“)",
        OptAllDates = "Alle Datumsangaben",
        CardTerms = "📝 Eigene Begriffe",
        TermsHint = "Ein Begriff pro Zeile (z.B. Firmennamen, Projektnamen oder Namen ohne Anrede). Diese werden immer ersetzt und lokal im Browser gespeichert.",
        TermsPlaceholder = "Muster AG\nAnna Beispiel\nProjekt Phoenix",
        CardAllowed = "✅ Erlaubte Werte",
        AllowedHint = "Diese Werte werden nie ersetzt, auch wenn die Erkennung sie findet. Ein Wert pro Zeile – oder in der Zuordnungstabelle auf 👁 klicken. Wird lokal im Browser gespeichert.",
        AllowTooltip = "Diesen Wert nicht ersetzen – im Text anzeigen",
        AllowedChipsTitle = "Erlaubt (wird angezeigt):",
        AllowedChipRemoveTooltip = "Wieder ersetzen",
        CardLlm = "🤖 KI-Erkennung (lokales LLM)",
        LlmEnable = "Lokales LLM zusätzlich verwenden",
        LlmHint = "Ein lokal laufendes Sprachmodell (via Ollama) versteht, was persönliche Daten sind, und findet auch Namen ohne Anrede, Firmen und Ähnliches, das kein Muster abdeckt. Der Text bleibt dabei zu 100 % auf diesem Rechner.",
        LlmStatusChecking = "Prüfe Ollama …",
        LlmStatusReady = "✅ Ollama erreichbar",
        LlmStatusOffline = "⚠️ Ollama nicht erreichbar",
        LlmOfflineHelp = "Ollama von ollama.com installieren und starten – den Rest erledigt die App automatisch (das Modell wird selbständig heruntergeladen).",
        LlmModelLabel = "Modell",
        LlmAnalyzing = "Das lokale Modell analysiert den Text – das kann je nach Rechner eine Weile dauern.",
        LlmError = "KI-Erkennung fehlgeschlagen – das Ergebnis basiert nur auf den Erkennungsregeln.",
        LlmStatusPulling = "⬇️ Modell {0} wird heruntergeladen … {1} %",
        LlmPullNote = "Passiert nur einmal – danach ist die KI-Erkennung sofort einsatzbereit.",
        LlmPullFailed = "Automatischer Download fehlgeschlagen – im Terminal „ollama pull {0}“ ausführen.",
        BtnRecheck = "↻ Erneut prüfen"
    };

    private static readonly UiText En = new()
    {
        PageTitle = "Data Anonymizer",
        AppTitle = "🔒 Data Anonymizer",
        Tagline = "Replaces personal data with placeholders before you paste a case into an AI tool – and puts the real data back afterwards.",
        PrivacyBadge = "🔒 Everything stays on this computer – no data is ever sent to the internet.",
        HowTitle = "How it works",
        Step1Title = "1. Anonymize",
        Step1Desc = "Paste text with personal data. The app replaces names, addresses etc. with placeholders like [NAME_1].",
        Step2Title = "2. Use in your AI tool",
        Step2Desc = "Paste the anonymized text into Claude, ChatGPT etc. and let it write the answer or SQL script.",
        Step3Title = "3. Translate back",
        Step3Desc = "Paste the answer here – the placeholders are replaced with your real data again.",
        AdvancedTitle = "⚙️ Advanced settings",
        AdvancedHint = "Everything is set up sensibly by default. Here you can choose what gets detected and add your own terms.",
        DsgNote = "Helps you handle personal data in line with the revised Swiss Data Protection Act (revDSG/nFADP), including special-category data. Detection is an aid and does not replace your own review; this is not legal advice.",
        CardOriginal = "1️⃣ Original text with personal data",
        InputPlaceholder = "Paste your case text here, e.g.:\nMr. John Smith (born on 12/03/1985), 12 Main Street, Zurich, phone +41 79 123 45 67, john.smith@example.com, reports a claim. Policy No. P-2023/4711, IBAN CH93 0076 2011 6238 5295 7.",
        BtnAnonymize = "🔒 Anonymize",
        BtnAnonymizeBusy = "⏳ Analyzing …",
        BtnClear = "Clear",
        BtnPaste = "📥 Paste",
        SummaryLine = "{0} values in {1} categories replaced",
        CardAnonymized = "2️⃣ Anonymized text – safe for Claude Code",
        BtnCopy = "📋 Copy",
        Copied = "✔ Copied",
        MappingTitle = "Mapping table ({0} values replaced)",
        ThPlaceholder = "Placeholder",
        ThOriginal = "Original value",
        ThCategory = "Category",
        MappingNote = "The table is stored locally in this browser until you delete it – so you can translate answers or scripts back later, too.",
        NoPiiFound = "No personal data found.",
        SensitiveWarning = "⚠️ Note: {0} item(s) of special-category data detected and replaced (revDSG). Please review with extra care.",
        MappingLoadedNote = "Stored mapping loaded ({0} entries) – paste the answer or script below and restore the values.",
        BtnExportMapping = "⬇ Export mapping",
        BtnImportMapping = "⬆ Import mapping",
        BtnDeleteMapping = "🗑 Delete mapping",
        CardDeanonymize = "3️⃣ De-anonymize the answer (restore placeholders)",
        DeanonHint = "Paste the answer from Claude Code here – the placeholders will be replaced with the original values again.",
        ResponsePlaceholder = "Paste the answer containing placeholders like [NAME_1] here …",
        BtnDeanonymize = "🔓 Restore original values",
        RestoredTitle = "Restored text",
        CardRules = "⚙️ Detection rules",
        OptNames = "Names (after Mr./Mrs., “Name:”, …)",
        OptEmails = "E-mail addresses",
        OptPhones = "Phone numbers",
        OptStreets = "Streets & house numbers",
        OptCities = "Postal codes & towns",
        OptIban = "IBAN",
        OptSsn = "Social security numbers (AHV/SSN)",
        OptCards = "Credit card numbers",
        OptRefs = "Customer/policy/case numbers",
        OptPlates = "License plates (CH)",
        OptOrgs = "Companies & organizations (AI only)",
        OptIp = "IP addresses",
        OptSensitive = "Special-category data (revDSG, AI only)",
        OptBirthdays = "Birth dates (“born on”, “DOB:”)",
        OptAllDates = "All dates",
        CardTerms = "📝 Custom terms",
        TermsHint = "One term per line (e.g. company names, project names or names without a salutation). These are always replaced and stored locally in your browser.",
        TermsPlaceholder = "Acme Ltd\nAnna Example\nProject Phoenix",
        CardAllowed = "✅ Allowed values",
        AllowedHint = "These values are never replaced, even when detection finds them. One value per line – or click 👁 in the mapping table. Stored locally in your browser.",
        AllowTooltip = "Do not replace this value – show it in the text",
        AllowedChipsTitle = "Allowed (shown as-is):",
        AllowedChipRemoveTooltip = "Replace again",
        CardLlm = "🤖 AI detection (local LLM)",
        LlmEnable = "Additionally use a local LLM",
        LlmHint = "A language model running locally (via Ollama) understands what personal data is and also finds names without salutations, companies and similar data that no pattern covers. Your text stays 100% on this machine.",
        LlmStatusChecking = "Checking Ollama …",
        LlmStatusReady = "✅ Ollama reachable",
        LlmStatusOffline = "⚠️ Ollama not reachable",
        LlmOfflineHelp = "Install Ollama from ollama.com and start it – the app does the rest automatically (the model is downloaded on its own).",
        LlmModelLabel = "Model",
        LlmAnalyzing = "The local model is analyzing the text – depending on your machine this can take a while.",
        LlmError = "AI detection failed – the result is based on the detection rules only.",
        LlmStatusPulling = "⬇️ Downloading model {0} … {1}%",
        LlmPullNote = "This happens only once – afterwards AI detection is ready right away.",
        LlmPullFailed = "Automatic download failed – run “ollama pull {0}” in a terminal.",
        BtnRecheck = "↻ Check again"
    };

    private static readonly UiText Fr = new()
    {
        PageTitle = "Anonymiseur de données",
        AppTitle = "🔒 Anonymiseur de données",
        Tagline = "Remplace les données personnelles par des variables avant de coller un cas dans un outil d'IA – et réinsère les vraies données ensuite.",
        PrivacyBadge = "🔒 Tout reste sur cet ordinateur – aucune donnée n'est envoyée sur Internet.",
        HowTitle = "Comment ça marche",
        Step1Title = "1. Anonymiser",
        Step1Desc = "Collez un texte avec des données personnelles. L'app remplace noms, adresses, etc. par des variables comme [NOM_1].",
        Step2Title = "2. Utiliser dans l'IA",
        Step2Desc = "Collez le texte anonymisé dans Claude, ChatGPT, etc. et laissez générer la réponse ou le script SQL.",
        Step3Title = "3. Retraduire",
        Step3Desc = "Collez la réponse ici – les variables sont remplacées par vos vraies données.",
        AdvancedTitle = "⚙️ Paramètres avancés",
        AdvancedHint = "Tout est configuré judicieusement par défaut. Ici, vous pouvez choisir ce qui est détecté et ajouter vos propres termes.",
        DsgNote = "Aide à traiter les données personnelles conformément à la nouvelle loi suisse sur la protection des données (nLPD/revDSG), y compris les données sensibles. La détection est une aide et ne remplace pas votre contrôle ; ceci n'est pas un conseil juridique.",
        CardOriginal = "1️⃣ Texte original avec données personnelles",
        InputPlaceholder = "Collez votre texte ici, p. ex. :\nMonsieur Jean Dupont (né le 12.03.1985), Rue de Lausanne 12, 1201 Genève, tél. +41 79 123 45 67, jean.dupont@example.ch, annonce un sinistre. N° de police P-2023/4711, IBAN CH93 0076 2011 6238 5295 7.",
        BtnAnonymize = "🔒 Anonymiser",
        BtnAnonymizeBusy = "⏳ Analyse en cours …",
        BtnClear = "Effacer",
        BtnPaste = "📥 Coller",
        SummaryLine = "{0} valeurs remplacées dans {1} catégories",
        CardAnonymized = "2️⃣ Texte anonymisé – sûr pour Claude Code",
        BtnCopy = "📋 Copier",
        Copied = "✔ Copié",
        MappingTitle = "Table de correspondance ({0} valeurs remplacées)",
        ThPlaceholder = "Variable",
        ThOriginal = "Valeur originale",
        ThCategory = "Catégorie",
        MappingNote = "La table est enregistrée localement dans ce navigateur jusqu'à sa suppression – vous pouvez donc retraduire des réponses ou des scripts plus tard.",
        NoPiiFound = "Aucune donnée personnelle trouvée.",
        SensitiveWarning = "⚠️ Attention : {0} donnée(s) sensible(s) détectée(s) et remplacée(s) (nLPD). Veuillez vérifier avec un soin particulier.",
        MappingLoadedNote = "Table de correspondance chargée ({0} entrées) – collez la réponse ou le script ci-dessous et restaurez les valeurs.",
        BtnExportMapping = "⬇ Exporter la table",
        BtnImportMapping = "⬆ Importer la table",
        BtnDeleteMapping = "🗑 Supprimer la table",
        CardDeanonymize = "3️⃣ Dé-anonymiser la réponse (restaurer les valeurs)",
        DeanonHint = "Collez ici la réponse de Claude Code – les variables seront remplacées par les valeurs originales.",
        ResponsePlaceholder = "Collez ici la réponse contenant des variables comme [NOM_1] …",
        BtnDeanonymize = "🔓 Restaurer les valeurs originales",
        RestoredTitle = "Texte restauré",
        CardRules = "⚙️ Règles de détection",
        OptNames = "Noms (après M./Mme, « Nom : », …)",
        OptEmails = "Adresses e-mail",
        OptPhones = "Numéros de téléphone",
        OptStreets = "Rues et numéros",
        OptCities = "Codes postaux et localités",
        OptIban = "IBAN",
        OptSsn = "Numéros AVS / sécurité sociale",
        OptCards = "Numéros de carte de crédit",
        OptRefs = "N° de client/police/dossier",
        OptPlates = "Plaques d'immatriculation (CH)",
        OptOrgs = "Entreprises et organisations (IA seulement)",
        OptIp = "Adresses IP",
        OptSensitive = "Données sensibles (nLPD, IA seulement)",
        OptBirthdays = "Dates de naissance (« né le », …)",
        OptAllDates = "Toutes les dates",
        CardTerms = "📝 Termes personnalisés",
        TermsHint = "Un terme par ligne (p. ex. noms d'entreprises, de projets ou noms sans civilité). Ils sont toujours remplacés et enregistrés localement dans votre navigateur.",
        TermsPlaceholder = "Exemple SA\nAnna Exemple\nProjet Phoenix",
        CardAllowed = "✅ Valeurs autorisées",
        AllowedHint = "Ces valeurs ne sont jamais remplacées, même si la détection les trouve. Une valeur par ligne – ou cliquez sur 👁 dans la table de correspondance. Enregistré localement dans votre navigateur.",
        AllowTooltip = "Ne pas remplacer cette valeur – l'afficher dans le texte",
        AllowedChipsTitle = "Autorisé (affiché tel quel) :",
        AllowedChipRemoveTooltip = "Remplacer à nouveau",
        CardLlm = "🤖 Détection IA (LLM local)",
        LlmEnable = "Utiliser en plus un LLM local",
        LlmHint = "Un modèle de langue exécuté localement (via Ollama) comprend ce que sont les données personnelles et trouve aussi les noms sans civilité, les entreprises et d'autres données qu'aucun modèle ne couvre. Votre texte reste à 100 % sur cette machine.",
        LlmStatusChecking = "Vérification d'Ollama …",
        LlmStatusReady = "✅ Ollama accessible",
        LlmStatusOffline = "⚠️ Ollama inaccessible",
        LlmOfflineHelp = "Installez Ollama depuis ollama.com et démarrez-le – l'application s'occupe du reste (le modèle est téléchargé automatiquement).",
        LlmModelLabel = "Modèle",
        LlmAnalyzing = "Le modèle local analyse le texte – cela peut prendre un moment selon votre machine.",
        LlmError = "La détection IA a échoué – le résultat repose uniquement sur les règles de détection.",
        LlmStatusPulling = "⬇️ Téléchargement du modèle {0} … {1} %",
        LlmPullNote = "Cette étape n'a lieu qu'une seule fois – ensuite la détection IA est immédiatement prête.",
        LlmPullFailed = "Le téléchargement automatique a échoué – exécutez « ollama pull {0} » dans un terminal.",
        BtnRecheck = "↻ Revérifier"
    };

    private static readonly UiText It = new()
    {
        PageTitle = "Anonimizzatore di dati",
        AppTitle = "🔒 Anonimizzatore di dati",
        Tagline = "Sostituisce i dati personali con segnaposto prima di incollare un caso in uno strumento di IA – e reinserisce i dati reali dopo.",
        PrivacyBadge = "🔒 Tutto rimane su questo computer – nessun dato viene inviato a Internet.",
        HowTitle = "Come funziona",
        Step1Title = "1. Anonimizza",
        Step1Desc = "Incolla un testo con dati personali. L'app sostituisce nomi, indirizzi ecc. con segnaposto come [NOME_1].",
        Step2Title = "2. Usa nell'IA",
        Step2Desc = "Incolla il testo anonimizzato in Claude, ChatGPT ecc. e fai generare la risposta o lo script SQL.",
        Step3Title = "3. Ritraduci",
        Step3Desc = "Incolla la risposta qui – i segnaposto vengono sostituiti di nuovo con i tuoi dati reali.",
        AdvancedTitle = "⚙️ Impostazioni avanzate",
        AdvancedHint = "Tutto è impostato in modo sensato per impostazione predefinita. Qui puoi scegliere cosa viene rilevato e aggiungere i tuoi termini.",
        DsgNote = "Aiuta a trattare i dati personali secondo la legge svizzera riveduta sulla protezione dei dati (revDSG/nLPD), inclusi i dati sensibili. Il riconoscimento è un aiuto e non sostituisce il tuo controllo; questa non è una consulenza legale.",
        CardOriginal = "1️⃣ Testo originale con dati personali",
        InputPlaceholder = "Incolla qui il testo, ad es.:\nSignor Mario Rossi (nato il 12.03.1985), Via Roma 8, 6900 Lugano, tel. +41 79 123 45 67, mario.rossi@example.ch, segnala un sinistro. Polizza n. P-2023/4711, IBAN CH93 0076 2011 6238 5295 7.",
        BtnAnonymize = "🔒 Anonimizza",
        BtnAnonymizeBusy = "⏳ Analisi in corso …",
        BtnClear = "Svuota",
        BtnPaste = "📥 Incolla",
        SummaryLine = "{0} valori sostituiti in {1} categorie",
        CardAnonymized = "2️⃣ Testo anonimizzato – sicuro per Claude Code",
        BtnCopy = "📋 Copia",
        Copied = "✔ Copiato",
        MappingTitle = "Tabella di corrispondenza ({0} valori sostituiti)",
        ThPlaceholder = "Segnaposto",
        ThOriginal = "Valore originale",
        ThCategory = "Categoria",
        MappingNote = "La tabella viene salvata localmente in questo browser finché non la elimini – così puoi ritradurre risposte o script anche in seguito.",
        NoPiiFound = "Nessun dato personale trovato.",
        SensitiveWarning = "⚠️ Attenzione: {0} dato/i sensibile/i rilevato/i e sostituito/i (revDSG). Verifica con particolare cura.",
        MappingLoadedNote = "Tabella di corrispondenza caricata ({0} voci) – incolla sotto la risposta o lo script e ripristina i valori.",
        BtnExportMapping = "⬇ Esporta tabella",
        BtnImportMapping = "⬆ Importa tabella",
        BtnDeleteMapping = "🗑 Elimina tabella",
        CardDeanonymize = "3️⃣ De-anonimizzare la risposta (ripristinare i valori)",
        DeanonHint = "Incolla qui la risposta di Claude Code – i segnaposto verranno sostituiti di nuovo con i valori originali.",
        ResponsePlaceholder = "Incolla qui la risposta con segnaposto come [NOME_1] …",
        BtnDeanonymize = "🔓 Ripristina i valori originali",
        RestoredTitle = "Testo ripristinato",
        CardRules = "⚙️ Regole di riconoscimento",
        OptNames = "Nomi (dopo Sig./Sig.ra, «Nome:», …)",
        OptEmails = "Indirizzi e-mail",
        OptPhones = "Numeri di telefono",
        OptStreets = "Vie e numeri civici",
        OptCities = "CAP e località",
        OptIban = "IBAN",
        OptSsn = "Numeri AVS / previdenza sociale",
        OptCards = "Numeri di carta di credito",
        OptRefs = "N. cliente/polizza/pratica",
        OptPlates = "Targhe (CH)",
        OptOrgs = "Aziende e organizzazioni (solo IA)",
        OptIp = "Indirizzi IP",
        OptSensitive = "Dati sensibili (revDSG, solo IA)",
        OptBirthdays = "Date di nascita («nato il», …)",
        OptAllDates = "Tutte le date",
        CardTerms = "📝 Termini personalizzati",
        TermsHint = "Un termine per riga (ad es. nomi di aziende, di progetti o nomi senza titolo). Vengono sempre sostituiti e salvati localmente nel browser.",
        TermsPlaceholder = "Esempio SA\nAnna Esempio\nProgetto Phoenix",
        CardAllowed = "✅ Valori consentiti",
        AllowedHint = "Questi valori non vengono mai sostituiti, anche se il riconoscimento li trova. Un valore per riga – oppure clicca su 👁 nella tabella di corrispondenza. Salvato localmente nel browser.",
        AllowTooltip = "Non sostituire questo valore – mostralo nel testo",
        AllowedChipsTitle = "Consentito (mostrato così com'è):",
        AllowedChipRemoveTooltip = "Sostituisci di nuovo",
        CardLlm = "🤖 Riconoscimento IA (LLM locale)",
        LlmEnable = "Usa anche un LLM locale",
        LlmHint = "Un modello linguistico eseguito localmente (via Ollama) capisce cosa sono i dati personali e trova anche nomi senza titolo, aziende e dati simili che nessun pattern copre. Il testo rimane al 100 % su questo computer.",
        LlmStatusChecking = "Verifica di Ollama …",
        LlmStatusReady = "✅ Ollama raggiungibile",
        LlmStatusOffline = "⚠️ Ollama non raggiungibile",
        LlmOfflineHelp = "Installa Ollama da ollama.com e avvialo – al resto pensa l'app (il modello viene scaricato automaticamente).",
        LlmModelLabel = "Modello",
        LlmAnalyzing = "Il modello locale sta analizzando il testo – a seconda del computer può richiedere un po' di tempo.",
        LlmError = "Riconoscimento IA non riuscito – il risultato si basa solo sulle regole di riconoscimento.",
        LlmStatusPulling = "⬇️ Download del modello {0} … {1}%",
        LlmPullNote = "Succede solo una volta – dopo il riconoscimento IA è subito pronto.",
        LlmPullFailed = "Download automatico non riuscito – esegui «ollama pull {0}» in un terminale.",
        BtnRecheck = "↻ Ricontrolla"
    };
}
