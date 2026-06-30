namespace AIFileButler;

/// <summary>Built-in i18n. Set <see cref="Lang"/>, then call L.S("key").
/// Missing keys fall back to English, so partial translations are safe.</summary>
internal static class L
{
    public static string Lang = "en";

    // The most-spoken world languages (+ Romanian). Native names shown in the picker.
    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"), ("zh", "中文"), ("hi", "हिन्दी"), ("es", "Español"),
        ("fr", "Français"), ("ar", "العربية"), ("bn", "বাংলা"), ("pt", "Português"),
        ("ru", "Русский"), ("id", "Indonesia"), ("de", "Deutsch"), ("ja", "日本語"),
        ("ko", "한국어"), ("it", "Italiano"), ("tr", "Türkçe"), ("ro", "Română"),
    };

    public static string S(string key)
    {
        if (T.TryGetValue(Lang, out var d) && d.TryGetValue(key, out var v)) return v;
        return T["en"].TryGetValue(key, out var en) ? en : key;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> T = new()
    {
        ["en"] = new()
        {
            ["subtitle"] = "Local, private file organizer", ["language"] = "Language",
            ["folders"] = "Watched folders", ["add"] = "Add folder…", ["remove"] = "Remove",
            ["dest"] = "Destination folder (where sorted files go)", ["browse"] = "Browse…",
            ["model"] = "AI model (local, via Ollama)", ["test"] = "Test connection", ["testing"] = "Testing…",
            ["ai_ok"] = "✓ AI available — smart naming is on.",
            ["ai_no"] = "✗ Not reachable — install/start Ollama, otherwise rules mode.",
            ["model_hint"] = "Tip: a bigger model = more accurate categories, a little slower.",
            ["behavior"] = "Behavior", ["auto"] = "Auto-organize (move files automatically)",
            ["expiry_scan"] = "Scan documents for expiry dates & remind me (passport, visa, insurance…)",
            ["startup"] = "Start with Windows", ["dark_mode"] = "Dark mode",
            ["poll"] = "Check every (sec):", ["minage"] = "Min file age (sec):",
            ["review_below"] = "Set aside for review if confidence below (%):",
            ["sorting"] = "Sorting", ["music_by"] = "Sort music by", ["movie_by"] = "Sort movies by",
            ["photo_by"] = "Sort photos by",
            ["sep_parties"] = "Split invoices into Clients / Distributors",
            ["opt_none"] = "Don't sub-sort", ["opt_alpha"] = "Alphabetical (A–Z)", ["opt_year"] = "Year",
            ["opt_genre"] = "Genre", ["opt_artist"] = "Artist", ["opt_actor"] = "Lead actor",
            ["opt_date"] = "Date taken", ["opt_location"] = "Location", ["opt_person"] = "People (faces)",
            ["rules"] = "My rules", ["rule_match"] = "If name/content contains…",
            ["rule_folder"] = "→ put in folder", ["rule_add"] = "Add rule",
            ["rule_hint"] = "Your rules win over the AI. Folder can be nested, e.g. Invoices/Orange.",
            ["drop_hint"] = "⬇ Drop files anywhere on this window to organize them now",
            ["help"] = "Help", ["save"] = "Save", ["cancel"] = "Cancel",
            ["m_settings"] = "Settings…", ["m_auto"] = "Auto-organize", ["m_pause"] = "Pause",
            ["m_organize"] = "Organize now", ["m_undo"] = "Undo last batch",
            ["m_open"] = "Open sorted folder", ["m_history"] = "History…", ["m_people"] = "People…", ["m_reminders"] = "Reminders…", ["m_help"] = "Help", ["m_quit"] = "Quit",
            ["m_memories"] = "Memories…", ["m_open_window"] = "Open AI File Butler",
            ["nav_dashboard"] = "Dashboard", ["nav_settings"] = "Settings", ["nav_people"] = "People",
            ["nav_reminders"] = "Reminders", ["nav_memories"] = "Memories", ["nav_history"] = "History",
            ["dash_quick"] = "Quick actions", ["dash_library"] = "Your library", ["dash_upcoming"] = "Upcoming reminders",
            ["dash_none_up"] = "Nothing expiring soon — you're all set.",
            ["dash_ai_on"] = "Smart AI naming is ON", ["dash_ai_off"] = "Rules mode — install Ollama for smart naming",
            ["dash_mode_auto"] = "Auto-organizing", ["dash_mode_manual"] = "Manual mode",
            ["dash_session"] = "{0} file(s) organized this session", ["apply"] = "Apply",
            ["dash_resume"] = "Resume", ["dash_in_days"] = "in {0} day(s)", ["dash_expired"] = "expired",
            ["dash_photos"] = "Photos", ["dash_docs"] = "Documents",
            ["mem_title"] = "Memories", ["mem_summary"] = "Your library: {0} photos · {1} documents · {2} people",
            ["mem_onthisday"] = "On this day", ["mem_years"] = "{0} year(s) ago",
            ["mem_empty"] = "Photos you took on this day in past years will appear here.",
            ["n_onthisday"] = "📸 On this day, {0} year(s) ago: {1} photo(s).",
            ["exp_title"] = "Document reminders", ["exp_doc"] = "Document", ["exp_kind"] = "Type",
            ["exp_date"] = "Expires", ["exp_days"] = "Days left", ["exp_empty"] = "No documents with an expiry date yet.",
            ["exp_add"] = "Add document…",
            ["exp_added"] = "Added: {0} expires on {1}.",
            ["exp_none_found"] = "Couldn't find an expiry date in that document. Try a clearer one (passport, insurance, visa…).",
            ["n_expiring"] = "⏰ Your {0} expires in {1} day(s).", ["n_expired"] = "⏰ Your {0} has expired.",
            ["people_title"] = "People (face recognition)", ["ppl_name"] = "Person's name",
            ["ppl_add"] = "Add a photo of this person…", ["ppl_remove"] = "Remove selected",
            ["ppl_hint"] = "Add 2–3 clear photos per person, then set Settings → Sort photos by → People (faces).",
            ["ppl_noface"] = "No face was found in that photo. Try a clearer, front-facing one.",
            ["ppl_added"] = "Added a photo for {0}.",
            ["ppl_dl_hint"] = "Face recognition needs a one-time ~13 MB model download.",
            ["ppl_download"] = "Download model (~13 MB)", ["ppl_dl_fail"] = "Download failed. Check your internet and try again.",
            ["history_title"] = "History", ["hist_file"] = "File", ["hist_dest"] = "Moved to",
            ["hist_when"] = "When", ["hist_undo"] = "Undo selected", ["hist_empty"] = "Nothing organized yet.",
            ["n_running"] = "Running. Right-click the tray icon for options.",
            ["n_organized"] = "Organized {0} file(s): {1}", ["n_reverted"] = "Reverted {0} move(s)",
            ["n_learned"] = "Learned: '{0}' → {1}", ["n_nothing"] = "Nothing to organize right now.",
            ["n_startup_on"] = "Will start automatically with Windows.",
            ["n_startup_off"] = "Won't start automatically anymore.",
            ["t_reverted"] = "Reverted", ["t_error"] = "Butler error",
            ["welcome_title"] = "Welcome to AI File Butler 🤵",
            ["welcome_body"] =
                "Thanks for installing!\n\n" +
                "For your safety, the Butler starts in MANUAL mode — it will NOT move " +
                "any files automatically yet. Nothing happens until you choose to.\n\n" +
                "Recommended first steps:\n" +
                "1. Open Settings and pick the folder(s) to watch and your language.\n" +
                "2. Click \"Organize now\" once to see how it sorts your files.\n" +
                "3. When you're happy, turn on Auto-organize in Settings or from the tray menu.\n\n" +
                "Tip: install Ollama (ollama.com) for smart AI naming — without it the Butler still sorts by rules.",
            ["welcome_open"] = "Open Settings…", ["welcome_auto"] = "Turn on auto-organize",
            ["welcome_close"] = "Keep it manual for now",
            ["help_title"] = "How AI File Butler works",
            ["help_body"] =
                "AI File Butler keeps your folders tidy — automatically and 100% on your PC.\n\n" +
                "1. WATCHES the folders you choose (Downloads by default).\n" +
                "2. READS each new file — text, PDFs, and even photos/scans (OCR).\n" +
                "3. DECIDES the right category and a clean, descriptive new name, using a local AI model.\n" +
                "4. FILES it into the destination folder and notifies you.\n\n" +
                "• Smart mode (AI): install Ollama (ollama.com) and a model, e.g. 'ollama pull llama3.1:8b'.\n" +
                "• Rules mode: without Ollama it still sorts by file type and keywords.\n\n" +
                "PRIVACY: nothing is uploaded. The AI runs locally — your documents never leave your computer.\n\n" +
                "LEARNS: move a sorted file to another category and the Butler remembers it next time.\n\n" +
                "SAFE: it never overwrites, and every batch can be undone.",
        },
        ["ro"] = new()
        {
            ["subtitle"] = "Organizator de fișiere local și privat", ["language"] = "Limbă",
            ["folders"] = "Foldere urmărite", ["add"] = "Adaugă folder…", ["remove"] = "Șterge",
            ["dest"] = "Folder destinație (unde merg fișierele sortate)", ["browse"] = "Răsfoiește…",
            ["model"] = "Model AI (local, prin Ollama)", ["test"] = "Testează conexiunea", ["testing"] = "Se testează…",
            ["ai_ok"] = "✓ AI disponibil — redenumirea inteligentă e activă.",
            ["ai_no"] = "✗ Indisponibil — instalează/pornește Ollama, altfel merge pe reguli.",
            ["model_hint"] = "Sfat: un model mai mare = categorii mai precise, puțin mai lent.",
            ["behavior"] = "Comportament", ["auto"] = "Auto-organizare (mută fișierele automat)",
            ["expiry_scan"] = "Scanează documentele pentru date de expirare și amintește-mi (pașaport, viză, asigurări…)",
            ["startup"] = "Pornește cu Windows", ["dark_mode"] = "Mod întunecat",
            ["poll"] = "Verifică la (sec):", ["minage"] = "Vârsta minimă a fișierului (sec):",
            ["review_below"] = "Pune deoparte pentru verificare dacă încrederea e sub (%):",
            ["sorting"] = "Sortare", ["music_by"] = "Sortează muzica după", ["movie_by"] = "Sortează filmele după",
            ["photo_by"] = "Sortează pozele după",
            ["sep_parties"] = "Separă facturile în Clienți / Distribuitori",
            ["opt_none"] = "Fără sub-sortare", ["opt_alpha"] = "Alfabetic (A–Z)", ["opt_year"] = "An",
            ["opt_genre"] = "Gen", ["opt_artist"] = "Artist", ["opt_actor"] = "Actor principal",
            ["opt_date"] = "Data pozei", ["opt_location"] = "Locație", ["opt_person"] = "Persoane (fețe)",
            ["rules"] = "Regulile mele", ["rule_match"] = "Dacă numele/conținutul conține…",
            ["rule_folder"] = "→ pune în folderul", ["rule_add"] = "Adaugă regula",
            ["rule_hint"] = "Regulile tale au prioritate față de AI. Folderul poate fi imbricat, ex. Facturi/Orange.",
            ["drop_hint"] = "⬇ Trage fișiere oriunde pe această fereastră ca să le organizez acum",
            ["help"] = "Ajutor", ["save"] = "Salvează", ["cancel"] = "Anulează",
            ["m_settings"] = "Setări…", ["m_auto"] = "Auto-organizare", ["m_pause"] = "Pauză",
            ["m_organize"] = "Organizează acum", ["m_undo"] = "Anulează ultima tură",
            ["m_open"] = "Deschide folderul sortat", ["m_history"] = "Istoric…", ["m_people"] = "Persoane…", ["m_reminders"] = "Reminder-e…", ["m_help"] = "Ajutor", ["m_quit"] = "Ieșire",
            ["m_memories"] = "Amintiri…", ["m_open_window"] = "Deschide AI File Butler",
            ["nav_dashboard"] = "Panou", ["nav_settings"] = "Setări", ["nav_people"] = "Persoane",
            ["nav_reminders"] = "Reminder-e", ["nav_memories"] = "Amintiri", ["nav_history"] = "Istoric",
            ["dash_quick"] = "Acțiuni rapide", ["dash_library"] = "Biblioteca ta", ["dash_upcoming"] = "Reminder-e apropiate",
            ["dash_none_up"] = "Nimic care expiră curând — ești în regulă.",
            ["dash_ai_on"] = "Redenumire inteligentă AI: PORNITĂ", ["dash_ai_off"] = "Mod reguli — instalează Ollama pentru AI",
            ["dash_mode_auto"] = "Auto-organizare activă", ["dash_mode_manual"] = "Mod manual",
            ["dash_session"] = "{0} fișier(e) organizate în această sesiune", ["apply"] = "Aplică",
            ["dash_resume"] = "Reia", ["dash_in_days"] = "în {0} zi(le)", ["dash_expired"] = "expirat",
            ["dash_photos"] = "Poze", ["dash_docs"] = "Documente",
            ["mem_title"] = "Amintiri", ["mem_summary"] = "Biblioteca ta: {0} poze · {1} documente · {2} persoane",
            ["mem_onthisday"] = "În această zi", ["mem_years"] = "acum {0} an(i)",
            ["mem_empty"] = "Pozele făcute în această zi în anii trecuți vor apărea aici.",
            ["n_onthisday"] = "📸 În această zi, acum {0} an(i): {1} poză(e).",
            ["exp_title"] = "Reminder-e documente", ["exp_doc"] = "Document", ["exp_kind"] = "Tip",
            ["exp_date"] = "Expiră", ["exp_days"] = "Zile rămase", ["exp_empty"] = "Niciun document cu dată de expirare încă.",
            ["exp_add"] = "Adaugă document…",
            ["exp_added"] = "Am adăugat: {0} expiră pe {1}.",
            ["exp_none_found"] = "Nu am găsit o dată de expirare în acel document. Încearcă unul mai clar (pașaport, asigurare, viză…).",
            ["n_expiring"] = "⏰ {0} expiră în {1} zi(le).", ["n_expired"] = "⏰ {0} a expirat.",
            ["people_title"] = "Persoane (recunoaștere facială)", ["ppl_name"] = "Numele persoanei",
            ["ppl_add"] = "Adaugă o poză cu această persoană…", ["ppl_remove"] = "Șterge selecția",
            ["ppl_hint"] = "Adaugă 2–3 poze clare per persoană, apoi alege Setări → Sortează pozele după → Persoane (fețe).",
            ["ppl_noface"] = "Nu am găsit nicio față în acea poză. Încearcă una mai clară, din față.",
            ["ppl_added"] = "Am adăugat o poză pentru {0}.",
            ["ppl_dl_hint"] = "Recunoașterea facială are nevoie de o descărcare unică (~13 MB) a modelului.",
            ["ppl_download"] = "Descarcă modelul (~13 MB)", ["ppl_dl_fail"] = "Descărcarea a eșuat. Verifică internetul și încearcă din nou.",
            ["history_title"] = "Istoric", ["hist_file"] = "Fișier", ["hist_dest"] = "Mutat în",
            ["hist_when"] = "Când", ["hist_undo"] = "Anulează selecția", ["hist_empty"] = "Nimic organizat încă.",
            ["n_running"] = "Pornit. Click-dreapta pe iconiță pentru opțiuni.",
            ["n_organized"] = "Organizate {0} fișier(e): {1}", ["n_reverted"] = "Anulate {0} mutare(i)",
            ["n_learned"] = "Învățat: '{0}' → {1}", ["n_nothing"] = "Nimic de organizat acum.",
            ["n_startup_on"] = "Va porni automat cu Windows.",
            ["n_startup_off"] = "Nu va mai porni automat.",
            ["t_reverted"] = "Anulat", ["t_error"] = "Eroare Butler",
            ["welcome_title"] = "Bun venit la AI File Butler 🤵",
            ["welcome_body"] =
                "Mulțumim că l-ai instalat!\n\n" +
                "Pentru siguranța ta, Butler-ul pornește în mod MANUAL — NU va muta " +
                "niciun fișier automat deocamdată. Nu se întâmplă nimic până nu decizi tu.\n\n" +
                "Pași recomandați la început:\n" +
                "1. Deschide Setări și alege folderul/folderele de urmărit și limba.\n" +
                "2. Apasă o dată pe 'Organizează acum' ca să vezi cum îți sortează fișierele.\n" +
                "3. Când ești mulțumit, pornește Auto-organizarea din Setări sau din meniul tray.\n\n" +
                "Sfat: instalează Ollama (ollama.com) pentru redenumire inteligentă cu AI — fără el, Butler-ul tot sortează pe reguli.",
            ["welcome_open"] = "Deschide Setări…", ["welcome_auto"] = "Pornește auto-organizarea",
            ["welcome_close"] = "Lasă-l manual deocamdată",
            ["help_title"] = "Cum funcționează AI File Butler",
            ["help_body"] =
                "AI File Butler îți ține folderele ordonate — automat și 100% pe PC-ul tău.\n\n" +
                "1. URMĂREȘTE folderele alese (implicit Downloads).\n" +
                "2. CITEȘTE fiecare fișier nou — text, PDF-uri și chiar poze/scanuri (OCR).\n" +
                "3. DECIDE categoria potrivită și un nume nou, clar, folosind un model AI local.\n" +
                "4. ÎL MUTĂ în folderul destinație și te anunță.\n\n" +
                "• Mod inteligent (AI): instalează Ollama (ollama.com) și un model, ex. 'ollama pull llama3.1:8b'.\n" +
                "• Mod reguli: fără Ollama, tot sortează după tipul fișierului și cuvinte cheie.\n\n" +
                "CONFIDENȚIALITATE: nimic nu se încarcă. AI-ul rulează local — documentele tale nu părăsesc calculatorul.\n\n" +
                "ÎNVAȚĂ: muți un fișier în altă categorie și Butler-ul reține data viitoare.\n\n" +
                "SIGUR: nu suprascrie niciodată, iar fiecare tură poate fi anulată.",
        },
        ["es"] = new()
        {
            ["subtitle"] = "Organizador de archivos local y privado", ["language"] = "Idioma",
            ["folders"] = "Carpetas vigiladas", ["add"] = "Añadir carpeta…", ["remove"] = "Quitar",
            ["dest"] = "Carpeta de destino (a dónde van los archivos)", ["browse"] = "Examinar…",
            ["model"] = "Modelo de IA (local, vía Ollama)", ["test"] = "Probar conexión", ["testing"] = "Probando…",
            ["ai_ok"] = "✓ IA disponible — el renombrado inteligente está activo.",
            ["ai_no"] = "✗ No disponible — instala/inicia Ollama, si no, modo reglas.",
            ["model_hint"] = "Consejo: un modelo más grande = categorías más precisas, algo más lento.",
            ["behavior"] = "Comportamiento", ["auto"] = "Auto-organizar (mover archivos automáticamente)",
            ["startup"] = "Iniciar con Windows", ["poll"] = "Comprobar cada (seg):", ["minage"] = "Edad mínima del archivo (seg):",
            ["help"] = "Ayuda", ["save"] = "Guardar", ["cancel"] = "Cancelar",
            ["m_settings"] = "Ajustes…", ["m_auto"] = "Auto-organizar", ["m_pause"] = "Pausa",
            ["m_organize"] = "Organizar ahora", ["m_undo"] = "Deshacer último lote",
            ["m_open"] = "Abrir carpeta ordenada", ["m_help"] = "Ayuda", ["m_quit"] = "Salir",
            ["n_running"] = "En ejecución. Clic derecho en el icono para opciones.",
            ["n_organized"] = "Organizados {0} archivo(s): {1}", ["n_reverted"] = "Revertidos {0} movimiento(s)",
            ["n_learned"] = "Aprendido: '{0}' → {1}", ["n_nothing"] = "Nada que organizar ahora.",
            ["n_startup_on"] = "Se iniciará automáticamente con Windows.",
            ["n_startup_off"] = "Ya no se iniciará automáticamente.",
            ["t_reverted"] = "Revertido", ["t_error"] = "Error de Butler",
            ["help_title"] = "Cómo funciona AI File Butler",
            ["help_body"] =
                "AI File Butler mantiene tus carpetas ordenadas — automáticamente y 100% en tu PC.\n\n" +
                "1. VIGILA las carpetas elegidas (Descargas por defecto).\n" +
                "2. LEE cada archivo nuevo — texto, PDF e incluso fotos/escaneos (OCR).\n" +
                "3. DECIDE la categoría correcta y un nombre nuevo y claro, con un modelo de IA local.\n" +
                "4. LO ARCHIVA en la carpeta de destino y te avisa.\n\n" +
                "• Modo inteligente (IA): instala Ollama (ollama.com) y un modelo, p. ej. 'ollama pull llama3.1:8b'.\n" +
                "• Modo reglas: sin Ollama, ordena por tipo de archivo y palabras clave.\n\n" +
                "PRIVACIDAD: nada se sube. La IA se ejecuta localmente — tus documentos nunca salen del ordenador.\n\n" +
                "APRENDE: mueve un archivo a otra categoría y el Butler lo recuerda.\n\n" +
                "SEGURO: nunca sobrescribe, y cada lote se puede deshacer.",
        },
        ["fr"] = new()
        {
            ["subtitle"] = "Organisateur de fichiers local et privé", ["language"] = "Langue",
            ["folders"] = "Dossiers surveillés", ["add"] = "Ajouter un dossier…", ["remove"] = "Retirer",
            ["dest"] = "Dossier de destination (où vont les fichiers)", ["browse"] = "Parcourir…",
            ["model"] = "Modèle IA (local, via Ollama)", ["test"] = "Tester la connexion", ["testing"] = "Test en cours…",
            ["ai_ok"] = "✓ IA disponible — le renommage intelligent est actif.",
            ["ai_no"] = "✗ Injoignable — installez/démarrez Ollama, sinon mode règles.",
            ["model_hint"] = "Astuce : un modèle plus grand = catégories plus précises, un peu plus lent.",
            ["behavior"] = "Comportement", ["auto"] = "Auto-organiser (déplacer les fichiers automatiquement)",
            ["startup"] = "Démarrer avec Windows", ["poll"] = "Vérifier toutes les (s) :", ["minage"] = "Âge min. du fichier (s) :",
            ["help"] = "Aide", ["save"] = "Enregistrer", ["cancel"] = "Annuler",
            ["m_settings"] = "Paramètres…", ["m_auto"] = "Auto-organiser", ["m_pause"] = "Pause",
            ["m_organize"] = "Organiser maintenant", ["m_undo"] = "Annuler le dernier lot",
            ["m_open"] = "Ouvrir le dossier trié", ["m_help"] = "Aide", ["m_quit"] = "Quitter",
            ["n_running"] = "En cours. Clic droit sur l'icône pour les options.",
            ["n_organized"] = "{0} fichier(s) organisé(s) : {1}", ["n_reverted"] = "{0} déplacement(s) annulé(s)",
            ["n_learned"] = "Appris : '{0}' → {1}", ["n_nothing"] = "Rien à organiser pour l'instant.",
            ["n_startup_on"] = "Démarrera automatiquement avec Windows.",
            ["n_startup_off"] = "Ne démarrera plus automatiquement.",
            ["t_reverted"] = "Annulé", ["t_error"] = "Erreur Butler",
            ["help_title"] = "Comment fonctionne AI File Butler",
            ["help_body"] =
                "AI File Butler range vos dossiers — automatiquement et 100% sur votre PC.\n\n" +
                "1. SURVEILLE les dossiers choisis (Téléchargements par défaut).\n" +
                "2. LIT chaque nouveau fichier — texte, PDF, et même photos/scans (OCR).\n" +
                "3. DÉCIDE la bonne catégorie et un nouveau nom clair, via un modèle IA local.\n" +
                "4. LE CLASSE dans le dossier de destination et vous notifie.\n\n" +
                "• Mode intelligent (IA) : installez Ollama (ollama.com) et un modèle, ex. 'ollama pull llama3.1:8b'.\n" +
                "• Mode règles : sans Ollama, le tri se fait par type et mots-clés.\n\n" +
                "CONFIDENTIALITÉ : rien n'est envoyé. L'IA s'exécute localement — vos documents ne quittent jamais l'ordinateur.\n\n" +
                "APPREND : déplacez un fichier dans une autre catégorie et le Butler s'en souvient.\n\n" +
                "SÛR : n'écrase jamais, et chaque lot est annulable.",
        },
        ["de"] = new()
        {
            ["subtitle"] = "Lokaler, privater Datei-Organizer", ["language"] = "Sprache",
            ["folders"] = "Überwachte Ordner", ["add"] = "Ordner hinzufügen…", ["remove"] = "Entfernen",
            ["dest"] = "Zielordner (wohin sortierte Dateien kommen)", ["browse"] = "Durchsuchen…",
            ["model"] = "KI-Modell (lokal, über Ollama)", ["test"] = "Verbindung testen", ["testing"] = "Wird getestet…",
            ["ai_ok"] = "✓ KI verfügbar — intelligente Benennung aktiv.",
            ["ai_no"] = "✗ Nicht erreichbar — Ollama installieren/starten, sonst Regelmodus.",
            ["model_hint"] = "Tipp: größeres Modell = genauere Kategorien, etwas langsamer.",
            ["behavior"] = "Verhalten", ["auto"] = "Auto-organisieren (Dateien automatisch verschieben)",
            ["startup"] = "Mit Windows starten", ["poll"] = "Prüfen alle (Sek.):", ["minage"] = "Mindestalter der Datei (Sek.):",
            ["help"] = "Hilfe", ["save"] = "Speichern", ["cancel"] = "Abbrechen",
            ["m_settings"] = "Einstellungen…", ["m_auto"] = "Auto-organisieren", ["m_pause"] = "Pause",
            ["m_organize"] = "Jetzt organisieren", ["m_undo"] = "Letzte Aktion rückgängig",
            ["m_open"] = "Sortierten Ordner öffnen", ["m_help"] = "Hilfe", ["m_quit"] = "Beenden",
            ["n_running"] = "Läuft. Rechtsklick auf das Symbol für Optionen.",
            ["n_organized"] = "{0} Datei(en) organisiert: {1}", ["n_reverted"] = "{0} Verschiebung(en) rückgängig",
            ["n_learned"] = "Gelernt: '{0}' → {1}", ["n_nothing"] = "Derzeit nichts zu organisieren.",
            ["n_startup_on"] = "Startet automatisch mit Windows.",
            ["n_startup_off"] = "Startet nicht mehr automatisch.",
            ["t_reverted"] = "Rückgängig", ["t_error"] = "Butler-Fehler",
            ["help_title"] = "So funktioniert AI File Butler",
            ["help_body"] =
                "AI File Butler hält deine Ordner ordentlich — automatisch und zu 100% auf deinem PC.\n\n" +
                "1. ÜBERWACHT die gewählten Ordner (standardmäßig Downloads).\n" +
                "2. LIEST jede neue Datei — Text, PDFs und sogar Fotos/Scans (OCR).\n" +
                "3. ENTSCHEIDET Kategorie und einen klaren Namen mit einem lokalen KI-Modell.\n" +
                "4. LEGT sie im Zielordner ab und benachrichtigt dich.\n\n" +
                "• Intelligenter Modus (KI): Ollama (ollama.com) und ein Modell installieren, z. B. 'ollama pull llama3.1:8b'.\n" +
                "• Regelmodus: ohne Ollama wird nach Dateityp und Schlüsselwörtern sortiert.\n\n" +
                "DATENSCHUTZ: nichts wird hochgeladen. Die KI läuft lokal — deine Dokumente verlassen den Computer nie.\n\n" +
                "LERNT: verschiebe eine Datei in eine andere Kategorie und der Butler merkt es sich.\n\n" +
                "SICHER: überschreibt nie, und jede Aktion ist umkehrbar.",
        },
        ["it"] = new()
        {
            ["subtitle"] = "Organizzatore di file locale e privato", ["language"] = "Lingua",
            ["folders"] = "Cartelle monitorate", ["add"] = "Aggiungi cartella…", ["remove"] = "Rimuovi",
            ["dest"] = "Cartella di destinazione (dove vanno i file)", ["browse"] = "Sfoglia…",
            ["model"] = "Modello IA (locale, via Ollama)", ["test"] = "Prova connessione", ["testing"] = "Verifica…",
            ["ai_ok"] = "✓ IA disponibile — la rinomina intelligente è attiva.",
            ["ai_no"] = "✗ Non raggiungibile — installa/avvia Ollama, altrimenti modalità regole.",
            ["model_hint"] = "Suggerimento: un modello più grande = categorie più precise, un po' più lento.",
            ["behavior"] = "Comportamento", ["auto"] = "Auto-organizza (sposta i file automaticamente)",
            ["startup"] = "Avvia con Windows", ["poll"] = "Controlla ogni (sec):", ["minage"] = "Età minima del file (sec):",
            ["help"] = "Aiuto", ["save"] = "Salva", ["cancel"] = "Annulla",
            ["m_settings"] = "Impostazioni…", ["m_auto"] = "Auto-organizza", ["m_pause"] = "Pausa",
            ["m_organize"] = "Organizza ora", ["m_undo"] = "Annulla ultimo lotto",
            ["m_open"] = "Apri cartella ordinata", ["m_help"] = "Aiuto", ["m_quit"] = "Esci",
            ["n_running"] = "In esecuzione. Clic destro sull'icona per le opzioni.",
            ["n_organized"] = "{0} file organizzati: {1}", ["n_reverted"] = "{0} spostamento/i annullati",
            ["n_learned"] = "Imparato: '{0}' → {1}", ["n_nothing"] = "Niente da organizzare ora.",
            ["n_startup_on"] = "Si avvierà automaticamente con Windows.",
            ["n_startup_off"] = "Non si avvierà più automaticamente.",
            ["t_reverted"] = "Annullato", ["t_error"] = "Errore Butler",
            ["help_title"] = "Come funziona AI File Butler",
            ["help_body"] =
                "AI File Butler tiene in ordine le tue cartelle — automaticamente e 100% sul tuo PC.\n\n" +
                "1. MONITORA le cartelle scelte (Download per impostazione predefinita).\n" +
                "2. LEGGE ogni nuovo file — testo, PDF e persino foto/scansioni (OCR).\n" +
                "3. DECIDE la categoria giusta e un nome chiaro, con un modello IA locale.\n" +
                "4. LO ARCHIVIA nella cartella di destinazione e ti avvisa.\n\n" +
                "• Modalità intelligente (IA): installa Ollama (ollama.com) e un modello, es. 'ollama pull llama3.1:8b'.\n" +
                "• Modalità regole: senza Ollama, ordina per tipo di file e parole chiave.\n\n" +
                "PRIVACY: niente viene caricato. L'IA gira in locale — i tuoi documenti non lasciano mai il computer.\n\n" +
                "IMPARA: sposta un file in un'altra categoria e il Butler lo ricorda.\n\n" +
                "SICURO: non sovrascrive mai, e ogni lotto è annullabile.",
        },
        ["pt"] = new()
        {
            ["subtitle"] = "Organizador de arquivos local e privado", ["language"] = "Idioma",
            ["folders"] = "Pastas monitoradas", ["add"] = "Adicionar pasta…", ["remove"] = "Remover",
            ["dest"] = "Pasta de destino (para onde vão os arquivos)", ["browse"] = "Procurar…",
            ["model"] = "Modelo de IA (local, via Ollama)", ["test"] = "Testar conexão", ["testing"] = "Testando…",
            ["ai_ok"] = "✓ IA disponível — renomeação inteligente ativa.",
            ["ai_no"] = "✗ Indisponível — instale/inicie o Ollama, senão modo de regras.",
            ["model_hint"] = "Dica: um modelo maior = categorias mais precisas, um pouco mais lento.",
            ["behavior"] = "Comportamento", ["auto"] = "Auto-organizar (mover arquivos automaticamente)",
            ["startup"] = "Iniciar com o Windows", ["poll"] = "Verificar a cada (s):", ["minage"] = "Idade mín. do arquivo (s):",
            ["help"] = "Ajuda", ["save"] = "Salvar", ["cancel"] = "Cancelar",
            ["m_settings"] = "Configurações…", ["m_auto"] = "Auto-organizar", ["m_pause"] = "Pausar",
            ["m_organize"] = "Organizar agora", ["m_undo"] = "Desfazer último lote",
            ["m_open"] = "Abrir pasta ordenada", ["m_help"] = "Ajuda", ["m_quit"] = "Sair",
            ["n_running"] = "Em execução. Clique direito no ícone para opções.",
            ["n_organized"] = "{0} arquivo(s) organizado(s): {1}", ["n_reverted"] = "{0} movimentação(ões) desfeita(s)",
            ["n_learned"] = "Aprendido: '{0}' → {1}", ["n_nothing"] = "Nada para organizar agora.",
            ["n_startup_on"] = "Iniciará automaticamente com o Windows.",
            ["n_startup_off"] = "Não iniciará mais automaticamente.",
            ["t_reverted"] = "Desfeito", ["t_error"] = "Erro do Butler",
            ["help_title"] = "Como o AI File Butler funciona",
            ["help_body"] =
                "O AI File Butler mantém suas pastas organizadas — automaticamente e 100% no seu PC.\n\n" +
                "1. MONITORA as pastas escolhidas (Downloads por padrão).\n" +
                "2. LÊ cada novo arquivo — texto, PDFs e até fotos/digitalizações (OCR).\n" +
                "3. DECIDE a categoria certa e um nome claro, usando um modelo de IA local.\n" +
                "4. ARQUIVA na pasta de destino e avisa você.\n\n" +
                "• Modo inteligente (IA): instale o Ollama (ollama.com) e um modelo, ex. 'ollama pull llama3.1:8b'.\n" +
                "• Modo de regras: sem Ollama, ordena por tipo de arquivo e palavras-chave.\n\n" +
                "PRIVACIDADE: nada é enviado. A IA roda localmente — seus documentos nunca saem do computador.\n\n" +
                "APRENDE: mova um arquivo para outra categoria e o Butler lembra.\n\n" +
                "SEGURO: nunca sobrescreve, e cada lote pode ser desfeito.",
        },
        ["ru"] = new()
        {
            ["subtitle"] = "Локальный, приватный органайзер файлов", ["language"] = "Язык",
            ["folders"] = "Отслеживаемые папки", ["add"] = "Добавить папку…", ["remove"] = "Удалить",
            ["dest"] = "Папка назначения (куда идут отсортированные файлы)", ["browse"] = "Обзор…",
            ["model"] = "Модель ИИ (локально, через Ollama)", ["test"] = "Проверить соединение", ["testing"] = "Проверка…",
            ["ai_ok"] = "✓ ИИ доступен — умное переименование включено.",
            ["ai_no"] = "✗ Недоступно — установите/запустите Ollama, иначе режим правил.",
            ["model_hint"] = "Совет: модель побольше = точнее категории, чуть медленнее.",
            ["behavior"] = "Поведение", ["auto"] = "Авто-организация (перемещать файлы автоматически)",
            ["startup"] = "Запускать с Windows", ["poll"] = "Проверять каждые (сек):", ["minage"] = "Мин. возраст файла (сек):",
            ["help"] = "Справка", ["save"] = "Сохранить", ["cancel"] = "Отмена",
            ["m_settings"] = "Настройки…", ["m_auto"] = "Авто-организация", ["m_pause"] = "Пауза",
            ["m_organize"] = "Организовать сейчас", ["m_undo"] = "Отменить последнее",
            ["m_open"] = "Открыть папку", ["m_help"] = "Справка", ["m_quit"] = "Выход",
            ["n_running"] = "Запущено. Правый клик по значку — параметры.",
            ["n_organized"] = "Организовано файлов: {0}: {1}", ["n_reverted"] = "Отменено перемещений: {0}",
            ["n_learned"] = "Запомнено: '{0}' → {1}", ["n_nothing"] = "Сейчас нечего организовывать.",
            ["n_startup_on"] = "Будет запускаться автоматически с Windows.",
            ["n_startup_off"] = "Больше не будет запускаться автоматически.",
            ["t_reverted"] = "Отменено", ["t_error"] = "Ошибка Butler",
            ["help_title"] = "Как работает AI File Butler",
            ["help_body"] =
                "AI File Butler держит ваши папки в порядке — автоматически и на 100% на вашем ПК.\n\n" +
                "1. СЛЕДИТ за выбранными папками (по умолчанию Загрузки).\n" +
                "2. ЧИТАЕТ каждый новый файл — текст, PDF и даже фото/сканы (OCR).\n" +
                "3. ОПРЕДЕЛЯЕТ категорию и понятное новое имя с помощью локальной модели ИИ.\n" +
                "4. ПЕРЕМЕЩАЕТ в папку назначения и уведомляет вас.\n\n" +
                "• Умный режим (ИИ): установите Ollama (ollama.com) и модель, напр. 'ollama pull llama3.1:8b'.\n" +
                "• Режим правил: без Ollama сортирует по типу файла и ключевым словам.\n\n" +
                "ПРИВАТНОСТЬ: ничего не загружается. ИИ работает локально — ваши документы не покидают компьютер.\n\n" +
                "ОБУЧАЕТСЯ: переместите файл в другую категорию — и Butler запомнит.\n\n" +
                "БЕЗОПАСНО: никогда не перезаписывает, и любую партию можно отменить.",
        },
        ["zh"] = new()
        {
            ["subtitle"] = "本地、私密的文件整理工具", ["language"] = "语言",
            ["folders"] = "监视的文件夹", ["add"] = "添加文件夹…", ["remove"] = "移除",
            ["dest"] = "目标文件夹（整理后的文件去向）", ["browse"] = "浏览…",
            ["model"] = "AI 模型（本地，通过 Ollama）", ["test"] = "测试连接", ["testing"] = "测试中…",
            ["ai_ok"] = "✓ AI 可用 — 智能命名已开启。",
            ["ai_no"] = "✗ 无法连接 — 请安装/启动 Ollama，否则使用规则模式。",
            ["model_hint"] = "提示：更大的模型 = 分类更准确，但稍慢。",
            ["behavior"] = "行为", ["auto"] = "自动整理（自动移动文件）",
            ["startup"] = "随 Windows 启动", ["poll"] = "检查间隔（秒）：", ["minage"] = "文件最小年龄（秒）：",
            ["help"] = "帮助", ["save"] = "保存", ["cancel"] = "取消",
            ["m_settings"] = "设置…", ["m_auto"] = "自动整理", ["m_pause"] = "暂停",
            ["m_organize"] = "立即整理", ["m_undo"] = "撤销上一批",
            ["m_open"] = "打开整理文件夹", ["m_help"] = "帮助", ["m_quit"] = "退出",
            ["n_running"] = "正在运行。右键点击托盘图标查看选项。",
            ["n_organized"] = "已整理 {0} 个文件：{1}", ["n_reverted"] = "已撤销 {0} 次移动",
            ["n_learned"] = "已学习：'{0}' → {1}", ["n_nothing"] = "暂时没有可整理的文件。",
            ["n_startup_on"] = "将随 Windows 自动启动。",
            ["n_startup_off"] = "将不再自动启动。",
            ["t_reverted"] = "已撤销", ["t_error"] = "Butler 错误",
            ["help_title"] = "AI File Butler 的工作原理",
            ["help_body"] =
                "AI File Butler 让你的文件夹保持整洁 — 全自动，且 100% 在你的电脑上完成。\n\n" +
                "1. 监视你选择的文件夹（默认是“下载”）。\n" +
                "2. 读取每个新文件 — 文本、PDF，甚至照片/扫描件（OCR）。\n" +
                "3. 用本地 AI 模型判断正确的类别并生成清晰的新文件名。\n" +
                "4. 移动到目标文件夹并通知你。\n\n" +
                "• 智能模式（AI）：安装 Ollama (ollama.com) 和一个模型，例如 'ollama pull llama3.1:8b'。\n" +
                "• 规则模式：没有 Ollama 时，按文件类型和关键词整理。\n\n" +
                "隐私：不上传任何内容。AI 在本地运行 — 你的文档绝不离开电脑。\n\n" +
                "学习：把文件移到其他类别，Butler 下次会记住。\n\n" +
                "安全：从不覆盖文件，每一批都可以撤销。",
        },
        ["ja"] = new()
        {
            ["subtitle"] = "ローカルでプライベートなファイル整理ツール", ["language"] = "言語",
            ["folders"] = "監視するフォルダー", ["add"] = "フォルダーを追加…", ["remove"] = "削除",
            ["dest"] = "保存先フォルダー（整理後のファイル）", ["browse"] = "参照…",
            ["model"] = "AI モデル（ローカル、Ollama 経由）", ["test"] = "接続をテスト", ["testing"] = "テスト中…",
            ["ai_ok"] = "✓ AI 利用可能 — スマート命名が有効です。",
            ["ai_no"] = "✗ 接続できません — Ollama をインストール/起動してください。",
            ["model_hint"] = "ヒント：大きいモデルほど分類が正確ですが、少し遅くなります。",
            ["behavior"] = "動作", ["auto"] = "自動整理（ファイルを自動で移動）",
            ["startup"] = "Windows と一緒に起動", ["poll"] = "確認間隔（秒）：", ["minage"] = "ファイルの最小経過時間（秒）：",
            ["help"] = "ヘルプ", ["save"] = "保存", ["cancel"] = "キャンセル",
            ["m_settings"] = "設定…", ["m_auto"] = "自動整理", ["m_pause"] = "一時停止",
            ["m_organize"] = "今すぐ整理", ["m_undo"] = "直前の操作を元に戻す",
            ["m_open"] = "整理先フォルダーを開く", ["m_help"] = "ヘルプ", ["m_quit"] = "終了",
            ["n_running"] = "実行中。トレイアイコンを右クリックでオプション。",
            ["n_organized"] = "{0} 件のファイルを整理：{1}", ["n_reverted"] = "{0} 件の移動を元に戻しました",
            ["n_learned"] = "学習しました：'{0}' → {1}", ["n_nothing"] = "今は整理するものがありません。",
            ["n_startup_on"] = "Windows と一緒に自動起動します。",
            ["n_startup_off"] = "自動起動しなくなります。",
            ["t_reverted"] = "元に戻しました", ["t_error"] = "Butler エラー",
            ["help_title"] = "AI File Butler の仕組み",
            ["help_body"] =
                "AI File Butler はフォルダーを自動で、しかも 100% あなたの PC 上で整理します。\n\n" +
                "1. 選んだフォルダーを監視します（既定はダウンロード）。\n" +
                "2. 新しいファイルを読み取ります — テキスト、PDF、写真/スキャン（OCR）も。\n" +
                "3. ローカル AI モデルで適切なカテゴリと分かりやすい新しい名前を決めます。\n" +
                "4. 保存先フォルダーへ移動し、通知します。\n\n" +
                "• スマートモード（AI）：Ollama (ollama.com) とモデルを入れます。例 'ollama pull llama3.1:8b'。\n" +
                "• ルールモード：Ollama なしでもファイル種別とキーワードで整理します。\n\n" +
                "プライバシー：何もアップロードしません。AI はローカルで動作 — 文書が PC を離れることはありません。\n\n" +
                "学習：ファイルを別のカテゴリへ移すと、次回から Butler が覚えます。\n\n" +
                "安全：上書きはせず、どの操作も元に戻せます。",
        },
        ["ko"] = new()
        {
            ["subtitle"] = "로컬·비공개 파일 정리 도구", ["language"] = "언어",
            ["folders"] = "감시 폴더", ["add"] = "폴더 추가…", ["remove"] = "제거",
            ["dest"] = "대상 폴더 (정리된 파일이 가는 곳)", ["browse"] = "찾아보기…",
            ["model"] = "AI 모델 (로컬, Ollama 사용)", ["test"] = "연결 테스트", ["testing"] = "테스트 중…",
            ["ai_ok"] = "✓ AI 사용 가능 — 스마트 이름 변경이 켜졌습니다.",
            ["ai_no"] = "✗ 연결 불가 — Ollama를 설치/실행하세요. 아니면 규칙 모드.",
            ["model_hint"] = "팁: 더 큰 모델 = 더 정확한 분류, 약간 느림.",
            ["behavior"] = "동작", ["auto"] = "자동 정리 (파일 자동 이동)",
            ["startup"] = "Windows와 함께 시작", ["poll"] = "확인 주기 (초):", ["minage"] = "최소 파일 경과 시간 (초):",
            ["help"] = "도움말", ["save"] = "저장", ["cancel"] = "취소",
            ["m_settings"] = "설정…", ["m_auto"] = "자동 정리", ["m_pause"] = "일시정지",
            ["m_organize"] = "지금 정리", ["m_undo"] = "마지막 작업 취소",
            ["m_open"] = "정리 폴더 열기", ["m_help"] = "도움말", ["m_quit"] = "종료",
            ["n_running"] = "실행 중. 트레이 아이콘을 우클릭하면 옵션이 있습니다.",
            ["n_organized"] = "{0}개 파일 정리: {1}", ["n_reverted"] = "{0}개 이동 취소됨",
            ["n_learned"] = "학습함: '{0}' → {1}", ["n_nothing"] = "지금 정리할 것이 없습니다.",
            ["n_startup_on"] = "Windows와 함께 자동으로 시작됩니다.",
            ["n_startup_off"] = "더 이상 자동으로 시작되지 않습니다.",
            ["t_reverted"] = "취소됨", ["t_error"] = "Butler 오류",
            ["help_title"] = "AI File Butler 작동 방식",
            ["help_body"] =
                "AI File Butler는 폴더를 자동으로, 그리고 100% 당신의 PC에서 정리합니다.\n\n" +
                "1. 선택한 폴더를 감시합니다 (기본값: 다운로드).\n" +
                "2. 새 파일을 읽습니다 — 텍스트, PDF, 사진/스캔(OCR)까지.\n" +
                "3. 로컬 AI 모델로 올바른 분류와 깔끔한 새 이름을 정합니다.\n" +
                "4. 대상 폴더로 옮기고 알려줍니다.\n\n" +
                "• 스마트 모드(AI): Ollama (ollama.com)와 모델을 설치하세요. 예: 'ollama pull llama3.1:8b'.\n" +
                "• 규칙 모드: Ollama 없이도 파일 형식과 키워드로 정리합니다.\n\n" +
                "개인정보: 아무것도 업로드하지 않습니다. AI는 로컬에서 실행 — 문서가 PC를 떠나지 않습니다.\n\n" +
                "학습: 파일을 다른 분류로 옮기면 Butler가 다음에 기억합니다.\n\n" +
                "안전: 절대 덮어쓰지 않으며, 모든 작업을 취소할 수 있습니다.",
        },
        ["tr"] = new()
        {
            ["subtitle"] = "Yerel, gizli dosya düzenleyici", ["language"] = "Dil",
            ["folders"] = "İzlenen klasörler", ["add"] = "Klasör ekle…", ["remove"] = "Kaldır",
            ["dest"] = "Hedef klasör (sıralanan dosyalar nereye gider)", ["browse"] = "Gözat…",
            ["model"] = "AI modeli (yerel, Ollama ile)", ["test"] = "Bağlantıyı test et", ["testing"] = "Test ediliyor…",
            ["ai_ok"] = "✓ AI kullanılabilir — akıllı adlandırma açık.",
            ["ai_no"] = "✗ Erişilemiyor — Ollama'yı kurun/başlatın, yoksa kural modu.",
            ["model_hint"] = "İpucu: daha büyük model = daha doğru kategoriler, biraz daha yavaş.",
            ["behavior"] = "Davranış", ["auto"] = "Otomatik düzenle (dosyaları otomatik taşı)",
            ["startup"] = "Windows ile başlat", ["poll"] = "Kontrol sıklığı (sn):", ["minage"] = "Min. dosya yaşı (sn):",
            ["help"] = "Yardım", ["save"] = "Kaydet", ["cancel"] = "İptal",
            ["m_settings"] = "Ayarlar…", ["m_auto"] = "Otomatik düzenle", ["m_pause"] = "Duraklat",
            ["m_organize"] = "Şimdi düzenle", ["m_undo"] = "Son grubu geri al",
            ["m_open"] = "Sıralanan klasörü aç", ["m_help"] = "Yardım", ["m_quit"] = "Çıkış",
            ["n_running"] = "Çalışıyor. Seçenekler için simgeye sağ tıklayın.",
            ["n_organized"] = "{0} dosya düzenlendi: {1}", ["n_reverted"] = "{0} taşıma geri alındı",
            ["n_learned"] = "Öğrenildi: '{0}' → {1}", ["n_nothing"] = "Şu an düzenlenecek bir şey yok.",
            ["n_startup_on"] = "Windows ile otomatik başlayacak.",
            ["n_startup_off"] = "Artık otomatik başlamayacak.",
            ["t_reverted"] = "Geri alındı", ["t_error"] = "Butler hatası",
            ["help_title"] = "AI File Butler nasıl çalışır",
            ["help_body"] =
                "AI File Butler klasörlerinizi düzenli tutar — otomatik ve %100 kendi bilgisayarınızda.\n\n" +
                "1. Seçtiğiniz klasörleri İZLER (varsayılan: İndirilenler).\n" +
                "2. Her yeni dosyayı OKUR — metin, PDF ve hatta fotoğraf/tarama (OCR).\n" +
                "3. Yerel bir AI modeliyle doğru kategoriye ve net bir yeni ada KARAR verir.\n" +
                "4. Hedef klasöre TAŞIR ve sizi bilgilendirir.\n\n" +
                "• Akıllı mod (AI): Ollama (ollama.com) ve bir model kurun, ör. 'ollama pull llama3.1:8b'.\n" +
                "• Kural modu: Ollama olmadan da dosya türü ve anahtar kelimelere göre sıralar.\n\n" +
                "GİZLİLİK: hiçbir şey yüklenmez. AI yerelde çalışır — belgeleriniz bilgisayardan çıkmaz.\n\n" +
                "ÖĞRENİR: bir dosyayı başka kategoriye taşıyın, Butler bir dahakine hatırlar.\n\n" +
                "GÜVENLİ: asla üzerine yazmaz ve her grup geri alınabilir.",
        },
        ["id"] = new()
        {
            ["subtitle"] = "Pengatur file lokal & privat", ["language"] = "Bahasa",
            ["folders"] = "Folder yang dipantau", ["add"] = "Tambah folder…", ["remove"] = "Hapus",
            ["dest"] = "Folder tujuan (tempat file tersortir)", ["browse"] = "Telusuri…",
            ["model"] = "Model AI (lokal, via Ollama)", ["test"] = "Tes koneksi", ["testing"] = "Menguji…",
            ["ai_ok"] = "✓ AI tersedia — penamaan pintar aktif.",
            ["ai_no"] = "✗ Tidak terjangkau — pasang/jalankan Ollama, jika tidak mode aturan.",
            ["model_hint"] = "Tips: model lebih besar = kategori lebih akurat, sedikit lebih lambat.",
            ["behavior"] = "Perilaku", ["auto"] = "Auto-atur (pindahkan file otomatis)",
            ["startup"] = "Mulai bersama Windows", ["poll"] = "Periksa tiap (dtk):", ["minage"] = "Usia file min. (dtk):",
            ["help"] = "Bantuan", ["save"] = "Simpan", ["cancel"] = "Batal",
            ["m_settings"] = "Pengaturan…", ["m_auto"] = "Auto-atur", ["m_pause"] = "Jeda",
            ["m_organize"] = "Atur sekarang", ["m_undo"] = "Urungkan batch terakhir",
            ["m_open"] = "Buka folder tersortir", ["m_help"] = "Bantuan", ["m_quit"] = "Keluar",
            ["n_running"] = "Berjalan. Klik kanan ikon untuk opsi.",
            ["n_organized"] = "{0} file diatur: {1}", ["n_reverted"] = "{0} pemindahan diurungkan",
            ["n_learned"] = "Dipelajari: '{0}' → {1}", ["n_nothing"] = "Tidak ada yang perlu diatur sekarang.",
            ["n_startup_on"] = "Akan mulai otomatis bersama Windows.",
            ["n_startup_off"] = "Tidak akan mulai otomatis lagi.",
            ["t_reverted"] = "Diurungkan", ["t_error"] = "Galat Butler",
            ["help_title"] = "Cara kerja AI File Butler",
            ["help_body"] =
                "AI File Butler menjaga folder Anda tetap rapi — otomatis dan 100% di PC Anda.\n\n" +
                "1. MEMANTAU folder yang Anda pilih (default: Unduhan).\n" +
                "2. MEMBACA setiap file baru — teks, PDF, bahkan foto/pindaian (OCR).\n" +
                "3. MENENTUKAN kategori yang tepat dan nama baru yang jelas, dengan model AI lokal.\n" +
                "4. MEMINDAHKAN ke folder tujuan dan memberi tahu Anda.\n\n" +
                "• Mode pintar (AI): pasang Ollama (ollama.com) dan sebuah model, mis. 'ollama pull llama3.1:8b'.\n" +
                "• Mode aturan: tanpa Ollama tetap menyortir berdasarkan jenis file dan kata kunci.\n\n" +
                "PRIVASI: tidak ada yang diunggah. AI berjalan lokal — dokumen Anda tak pernah meninggalkan komputer.\n\n" +
                "BELAJAR: pindahkan file ke kategori lain dan Butler mengingatnya.\n\n" +
                "AMAN: tidak pernah menimpa, dan setiap batch bisa diurungkan.",
        },
        ["hi"] = new()
        {
            ["subtitle"] = "लोकल, निजी फ़ाइल आयोजक", ["language"] = "भाषा",
            ["folders"] = "निगरानी वाले फ़ोल्डर", ["add"] = "फ़ोल्डर जोड़ें…", ["remove"] = "हटाएँ",
            ["dest"] = "गंतव्य फ़ोल्डर (क्रमबद्ध फ़ाइलें कहाँ जाएँ)", ["browse"] = "ब्राउज़ करें…",
            ["model"] = "AI मॉडल (लोकल, Ollama के ज़रिए)", ["test"] = "कनेक्शन जाँचें", ["testing"] = "जाँच हो रही है…",
            ["ai_ok"] = "✓ AI उपलब्ध — स्मार्ट नामकरण चालू है।",
            ["ai_no"] = "✗ नहीं पहुँचा — Ollama इंस्टॉल/शुरू करें, वरना नियम मोड।",
            ["model_hint"] = "सुझाव: बड़ा मॉडल = अधिक सटीक श्रेणियाँ, थोड़ा धीमा।",
            ["behavior"] = "व्यवहार", ["auto"] = "स्वतः-व्यवस्थित (फ़ाइलें अपने आप ले जाएँ)",
            ["startup"] = "Windows के साथ शुरू करें", ["poll"] = "हर (सेकंड) जाँचें:", ["minage"] = "न्यूनतम फ़ाइल आयु (सेकंड):",
            ["help"] = "मदद", ["save"] = "सहेजें", ["cancel"] = "रद्द करें",
            ["m_settings"] = "सेटिंग्स…", ["m_auto"] = "स्वतः-व्यवस्थित", ["m_pause"] = "रोकें",
            ["m_organize"] = "अभी व्यवस्थित करें", ["m_undo"] = "अंतिम बैच पूर्ववत करें",
            ["m_open"] = "क्रमबद्ध फ़ोल्डर खोलें", ["m_help"] = "मदद", ["m_quit"] = "बंद करें",
            ["n_running"] = "चल रहा है। विकल्पों के लिए आइकन पर राइट-क्लिक करें।",
            ["n_organized"] = "{0} फ़ाइल(ें) व्यवस्थित: {1}", ["n_reverted"] = "{0} स्थानांतरण पूर्ववत किए गए",
            ["n_learned"] = "सीखा: '{0}' → {1}", ["n_nothing"] = "अभी व्यवस्थित करने के लिए कुछ नहीं है।",
            ["n_startup_on"] = "Windows के साथ अपने आप शुरू होगा।",
            ["n_startup_off"] = "अब अपने आप शुरू नहीं होगा।",
            ["t_reverted"] = "पूर्ववत", ["t_error"] = "Butler त्रुटि",
            ["help_title"] = "AI File Butler कैसे काम करता है",
            ["help_body"] =
                "AI File Butler आपके फ़ोल्डरों को साफ़-सुथरा रखता है — अपने आप और 100% आपके PC पर।\n\n" +
                "1. आपके चुने फ़ोल्डरों की निगरानी करता है (डिफ़ॉल्ट: डाउनलोड)।\n" +
                "2. हर नई फ़ाइल पढ़ता है — टेक्स्ट, PDF, यहाँ तक कि फ़ोटो/स्कैन (OCR)।\n" +
                "3. लोकल AI मॉडल से सही श्रेणी और स्पष्ट नया नाम तय करता है।\n" +
                "4. गंतव्य फ़ोल्डर में ले जाता है और आपको सूचित करता है।\n\n" +
                "• स्मार्ट मोड (AI): Ollama (ollama.com) और एक मॉडल इंस्टॉल करें, जैसे 'ollama pull llama3.1:8b'।\n" +
                "• नियम मोड: Ollama के बिना भी फ़ाइल प्रकार और कीवर्ड से क्रमबद्ध करता है।\n\n" +
                "गोपनीयता: कुछ भी अपलोड नहीं होता। AI लोकल चलता है — आपके दस्तावेज़ कंप्यूटर से बाहर नहीं जाते।\n\n" +
                "सीखता है: किसी फ़ाइल को दूसरी श्रेणी में ले जाएँ, Butler अगली बार याद रखेगा।\n\n" +
                "सुरक्षित: कभी अधिलेखित नहीं करता, और हर बैच पूर्ववत किया जा सकता है।",
        },
        ["bn"] = new()
        {
            ["subtitle"] = "লোকাল, ব্যক্তিগত ফাইল আয়োজক", ["language"] = "ভাষা",
            ["folders"] = "পর্যবেক্ষিত ফোল্ডার", ["add"] = "ফোল্ডার যোগ করুন…", ["remove"] = "সরান",
            ["dest"] = "গন্তব্য ফোল্ডার (সাজানো ফাইল কোথায় যাবে)", ["browse"] = "ব্রাউজ…",
            ["model"] = "AI মডেল (লোকাল, Ollama-এর মাধ্যমে)", ["test"] = "সংযোগ পরীক্ষা", ["testing"] = "পরীক্ষা চলছে…",
            ["ai_ok"] = "✓ AI উপলব্ধ — স্মার্ট নামকরণ চালু।",
            ["ai_no"] = "✗ সংযোগ নেই — Ollama ইনস্টল/চালু করুন, নয়তো নিয়ম মোড।",
            ["model_hint"] = "টিপ: বড় মডেল = আরও সঠিক বিভাগ, একটু ধীর।",
            ["behavior"] = "আচরণ", ["auto"] = "স্বয়ংক্রিয় সাজানো (ফাইল আপনাআপনি সরান)",
            ["startup"] = "Windows-এর সাথে চালু", ["poll"] = "প্রতি (সেকেন্ড) পরীক্ষা:", ["minage"] = "ন্যূনতম ফাইল বয়স (সেকেন্ড):",
            ["help"] = "সহায়তা", ["save"] = "সংরক্ষণ", ["cancel"] = "বাতিল",
            ["m_settings"] = "সেটিংস…", ["m_auto"] = "স্বয়ংক্রিয় সাজানো", ["m_pause"] = "বিরতি",
            ["m_organize"] = "এখনই সাজান", ["m_undo"] = "শেষ ব্যাচ পূর্বাবস্থায়",
            ["m_open"] = "সাজানো ফোল্ডার খুলুন", ["m_help"] = "সহায়তা", ["m_quit"] = "প্রস্থান",
            ["n_running"] = "চলছে। অপশনের জন্য আইকনে ডান-ক্লিক করুন।",
            ["n_organized"] = "{0} ফাইল সাজানো হয়েছে: {1}", ["n_reverted"] = "{0} স্থানান্তর পূর্বাবস্থায়",
            ["n_learned"] = "শেখা হয়েছে: '{0}' → {1}", ["n_nothing"] = "এখন সাজানোর কিছু নেই।",
            ["n_startup_on"] = "Windows-এর সাথে আপনাআপনি চালু হবে।",
            ["n_startup_off"] = "আর আপনাআপনি চালু হবে না।",
            ["t_reverted"] = "পূর্বাবস্থায়", ["t_error"] = "Butler ত্রুটি",
            ["help_title"] = "AI File Butler কীভাবে কাজ করে",
            ["help_body"] =
                "AI File Butler আপনার ফোল্ডার গুছিয়ে রাখে — স্বয়ংক্রিয়ভাবে এবং 100% আপনার PC-তে।\n\n" +
                "১. আপনার বেছে নেওয়া ফোল্ডার পর্যবেক্ষণ করে (ডিফল্ট: ডাউনলোড)।\n" +
                "২. প্রতিটি নতুন ফাইল পড়ে — টেক্সট, PDF, এমনকি ছবি/স্ক্যান (OCR)।\n" +
                "৩. লোকাল AI মডেল দিয়ে সঠিক বিভাগ ও পরিষ্কার নতুন নাম ঠিক করে।\n" +
                "৪. গন্তব্য ফোল্ডারে সরায় এবং আপনাকে জানায়।\n\n" +
                "• স্মার্ট মোড (AI): Ollama (ollama.com) ও একটি মডেল ইনস্টল করুন, যেমন 'ollama pull llama3.1:8b'।\n" +
                "• নিয়ম মোড: Ollama ছাড়াও ফাইলের ধরন ও কীওয়ার্ড দিয়ে সাজায়।\n\n" +
                "গোপনীয়তা: কিছুই আপলোড হয় না। AI লোকালি চলে — আপনার নথি কম্পিউটার ছাড়ে না।\n\n" +
                "শেখে: একটি ফাইল অন্য বিভাগে সরান, Butler পরেরবার মনে রাখবে।\n\n" +
                "নিরাপদ: কখনো ওভাররাইট করে না, এবং প্রতিটি ব্যাচ পূর্বাবস্থায় ফেরানো যায়।",
        },
        ["ar"] = new()
        {
            ["subtitle"] = "منظّم ملفات محلي وخاص", ["language"] = "اللغة",
            ["folders"] = "المجلدات المراقَبة", ["add"] = "إضافة مجلد…", ["remove"] = "إزالة",
            ["dest"] = "مجلد الوجهة (إلى أين تذهب الملفات المرتبة)", ["browse"] = "استعراض…",
            ["model"] = "نموذج الذكاء الاصطناعي (محلي، عبر Ollama)", ["test"] = "اختبار الاتصال", ["testing"] = "جارٍ الاختبار…",
            ["ai_ok"] = "✓ الذكاء الاصطناعي متاح — التسمية الذكية مفعّلة.",
            ["ai_no"] = "✗ غير متاح — ثبّت/شغّل Ollama، وإلا وضع القواعد.",
            ["model_hint"] = "نصيحة: نموذج أكبر = تصنيفات أدق، لكن أبطأ قليلاً.",
            ["behavior"] = "السلوك", ["auto"] = "تنظيم تلقائي (نقل الملفات تلقائياً)",
            ["startup"] = "البدء مع Windows", ["poll"] = "التحقق كل (ثانية):", ["minage"] = "أدنى عمر للملف (ثانية):",
            ["help"] = "مساعدة", ["save"] = "حفظ", ["cancel"] = "إلغاء",
            ["m_settings"] = "الإعدادات…", ["m_auto"] = "تنظيم تلقائي", ["m_pause"] = "إيقاف مؤقت",
            ["m_organize"] = "نظّم الآن", ["m_undo"] = "تراجع عن آخر دفعة",
            ["m_open"] = "فتح المجلد المرتب", ["m_help"] = "مساعدة", ["m_quit"] = "خروج",
            ["n_running"] = "قيد التشغيل. انقر بزر الفأرة الأيمن على الأيقونة للخيارات.",
            ["n_organized"] = "تم تنظيم {0} ملف: {1}", ["n_reverted"] = "تم التراجع عن {0} نقل",
            ["n_learned"] = "تم التعلّم: '{0}' ← {1}", ["n_nothing"] = "لا شيء لتنظيمه الآن.",
            ["n_startup_on"] = "سيبدأ تلقائياً مع Windows.",
            ["n_startup_off"] = "لن يبدأ تلقائياً بعد الآن.",
            ["t_reverted"] = "تراجع", ["t_error"] = "خطأ Butler",
            ["help_title"] = "كيف يعمل AI File Butler",
            ["help_body"] =
                "يبقي AI File Butler مجلداتك مرتبة — تلقائياً و100% على جهازك.\n\n" +
                "1. يراقب المجلدات التي تختارها (التنزيلات افتراضياً).\n" +
                "2. يقرأ كل ملف جديد — نص، PDF، وحتى الصور/المسح الضوئي (OCR).\n" +
                "3. يحدّد الفئة الصحيحة واسماً جديداً واضحاً باستخدام نموذج ذكاء اصطناعي محلي.\n" +
                "4. ينقله إلى مجلد الوجهة ويُعلمك.\n\n" +
                "• الوضع الذكي: ثبّت Ollama (ollama.com) ونموذجاً، مثل 'ollama pull llama3.1:8b'.\n" +
                "• وضع القواعد: بدون Ollama يرتّب حسب نوع الملف والكلمات المفتاحية.\n\n" +
                "الخصوصية: لا يُرفع أي شيء. يعمل الذكاء الاصطناعي محلياً — مستنداتك لا تغادر حاسوبك.\n\n" +
                "يتعلّم: انقل ملفاً إلى فئة أخرى وسيتذكّر Butler ذلك لاحقاً.\n\n" +
                "آمن: لا يستبدل الملفات أبداً، ويمكن التراجع عن كل دفعة.",
        },
    };
}
