export type Lang = 'sr' | 'en';

export interface Translations {
  finish: string;
  theme: { dark: string; light: string };

  loaderTitle: string;
  loaderSubtitle: string;
  repoUrlLabel: string;
  prNumberLabel: string;
  tokenLabel: string;
  loadPr: string;
  loading: string;
  repoUrlError: string;
  prNumberError: string;
  tokenError: string;
  invalidRepoUrl: string;
  genericError: string;

  changedFiles: string;
  noChangedFiles: string;
  statusAdded: string;
  statusRemoved: string;
  statusModified: string;

  loadingDiff: string;
  diffError: string;
  diffUnavailable: string;

  chatEmpty: string;
  chatPlaceholder: string;
  send: string;
  disclaimer: string;
  aiError: string;
  chips: string[];

  expandFileList: string;
  collapseFileList: string;
  expandDiff: string;
  collapseDiff: string;
  expandChat: string;
  collapseChat: string;

  loadRepoContext: string;
  repoContextLoading: string;
  repoContextLoaded: string;
  repoContextError: string;

  prDescription: string;
  prDescriptionEmpty: string;

  modeLabel: string;
  modeAiTitle: string;
  modeAiDesc: string;
  modeReportTitle: string;
  modeReportDesc: string;

  reportLoading: string;
  reportRetry: string;
  reportError: string;
  searchPlaceholder: string;
  searchNoMatches: string;

  finishModalTitle: string;
  finishModalCommentLabel: string;
  finishModalCommentPlaceholder: string;
  finishModalHint: string;
  finishModalAccept: string;
  finishModalReject: string;
  decisionError: string;

  summary: string;
  showFullDescription: string;

  quoteToChat: string;

  studyParticipantLabel: string;
  studyParticipantPlaceholder: string;
  studyParticipantRequired: string;
  studyParticipantNotFound: string;
  studyAllDone: string;
  studyLangLabel: string;
  studyLogin: string;
  studySessionLabel: string;
  tour: {
    next: string;
    back: string;
    skip: string;
    done: string;
    welcomeTitle: string;
    welcomeBody: string;
    fileListTitle: string;
    fileListBody: string;
    summaryTitle: string;
    summaryBody: string;
    diffTitle: string;
    diffBody: string;
    quoteTitle: string;
    quoteBody: string;
    chatTitle: string;
    chatBody: string;
    reportTitle: string;
    reportBody: string;
    searchTitle: string;
    searchBody: string;
    askQuestionTitle: string;
    askQuestionBody: string;
    switchToReportTitle: string;
    switchToReportBody: string;
    switchToAiTitle: string;
    switchToAiBody: string;
    decisionBtnTitle: string;
    decisionBtnBody: string;
    finishModalTitle: string;
    finishModalBody: string;
  };
}

export const translations: Record<Lang, Translations> = {
  sr: {
    finish: 'Donesi odluku',
    theme: { dark: 'Tamna', light: 'Svetla' },

    loaderTitle: 'Code Review AI',
    loaderSubtitle: 'Analiziraj Pull Request uz pomoć AI asistenta',
    repoUrlLabel: 'GitHub repozitorijum URL',
    prNumberLabel: 'Broj Pull Requesta',
    tokenLabel: 'GitHub Token',
    loadPr: 'Učitaj PR',
    loading: 'Učitavanje…',
    repoUrlError: 'Unesite validan GitHub URL (npr. https://github.com/owner/repo)',
    prNumberError: 'Unesite broj PR-a',
    tokenError: 'Token je obavezan',
    invalidRepoUrl: 'Nevažeći URL repozitorijuma. Primer: https://github.com/owner/repo',
    genericError: 'Došlo je do greške. Pokušajte ponovo.',

    changedFiles: 'Izmenjeni fajlovi',
    noChangedFiles: 'Nema izmenjenih fajlova.',
    statusAdded: 'dodato',
    statusRemoved: 'obrisano',
    statusModified: 'izmenjeno',

    loadingDiff: 'Učitavanje diffa…',
    diffError: 'Greška pri učitavanju diffa.',
    diffUnavailable: 'Diff nije dostupan za ovaj fajl (binarni fajl ili previše izmena).',

    chatEmpty: 'Postavite pitanje o ovom Pull Requestu koristeći unos ispod ili odaberite jedno od brzih pitanja.',
    chatPlaceholder: 'Postavite pitanje o ovom PR-u…',
    send: 'Pošalji',
    disclaimer: '⚠️ Ovaj alat pruža obrazovnu analizu. Konačnu odluku o PR-u donosi programer.',
    aiError: '_Greška pri komunikaciji s AI asistentom._',
    chips: [
      'Objasni šta radi ovaj PR ukratko',
      'Da li su poštovane SOLID principe?',
      'Postoje li sigurnosni problemi?',
      'Kako su pokriveni test slučajevi?'
    ],

    expandFileList: 'Proširi listu fajlova',
    collapseFileList: 'Smanji listu fajlova',
    expandDiff: 'Proširi diff pregled',
    collapseDiff: 'Smanji diff pregled',
    expandChat: 'Proširi chat',
    collapseChat: 'Smanji chat',

    loadRepoContext: 'Učitaj kontekst repozitorijuma',
    repoContextLoading: 'Učitavanje konteksta…',
    repoContextLoaded: 'Kontekst repozitorijuma učitan',
    repoContextError: 'Greška pri učitavanju konteksta repozitorijuma.',

    prDescription: 'Opis PR-a',
    prDescriptionEmpty: 'Ovaj PR nema opis.',

    modeLabel: 'Način pregleda',
    modeAiTitle: 'AI Mode',
    modeAiDesc: 'Postavljajte pitanja AI asistentu o PR-u u realnom vremenu',
    modeReportTitle: 'Wiki Mode',
    modeReportDesc: 'Dobijte detaljan pisani izveštaj o PR-u, bez chata',

    reportLoading: 'Generišem detaljan izveštaj o PR-u…',
    reportRetry: 'Pokušaj ponovo',
    reportError: 'Greška pri generisanju izveštaja.',
    searchPlaceholder: 'Pretraga u dokumentaciji…',
    searchNoMatches: 'Nema rezultata',

    finishModalTitle: 'Završite review',
    finishModalCommentLabel: 'Komentar o Pull Requestu',
    finishModalCommentPlaceholder: 'Unesite komentar o ovom Pull Requestu…',
    finishModalHint: 'Unesite komentar da biste nastavili.',
    finishModalAccept: 'Prihvati',
    finishModalReject: 'Odbaci',
    decisionError: 'Greška prilikom čuvanja odluke. Pokušajte ponovo.',

    summary: 'Sažetak',
    showFullDescription: 'Prikaži ceo opis →',

    quoteToChat: '💬 Citiraj u chat',

    studyParticipantLabel: 'Participant ID',
    studyParticipantPlaceholder: 'npr. 001',
    studyParticipantRequired: 'Unesite Participant ID',
    studyParticipantNotFound: 'Ispitanik sa ovim ID-em nije pronađen.',
    studyAllDone: 'Sve sesije za ovog ispitanika su završene. Hvala na učešću!',
    studyLangLabel: 'Jezik / Language',
    studyLogin: 'Prijavi se',
    studySessionLabel: 'Sesija',
    tour: {
      next: 'Dalje →',
      back: '← Nazad',
      skip: 'Preskoči tur',
      done: 'Razumem, hoću sam da probam',
      welcomeTitle: 'Dobrodošli u BeyondAI',
      welcomeBody: 'Ovaj kratak vodič će vas provesti kroz sve delove aplikacije koje ćete koristiti tokom pregleda Pull Requesta. Kliknite „Dalje" da nastavite, ili „Preskoči tur" ako želite odmah sami da istražujete.',
      fileListTitle: 'Lista izmenjenih fajlova',
      fileListBody: 'Ovde vidite sve fajlove koje ovaj Pull Request menja. Kliknite na fajl da otvorite njegove izmene (diff) u srednjem panelu.',
      summaryTitle: 'Sažetak PR-a',
      summaryBody: 'Kratak automatski sažetak Pull Requesta. Klikom na „Prikaži ceo opis" otvara se pun opis koji je autor PR-a napisao.',
      diffTitle: 'Pregled izmena (diff)',
      diffBody: 'Zeleno su dodate linije koda, crveno obrisane. Ovde čitate šta je tačno promenjeno u fajlu koji ste izabrali.',
      quoteTitle: 'Citiranje koda u chat',
      quoteBody: 'Ako selektujete deo koda mišem, pojaviće se dugme „Citiraj u chat" — tako možete pitati AI asistenta konkretno o toj liniji ili bloku koda.',
      chatTitle: 'Chat sa AI asistentom',
      chatBody: 'Ovde postavljate pitanja AI asistentu o Pull Requestu — slobodnim tekstom ili klikom na ponuđena brza pitanja. Asistent odgovara na osnovu koda i opisa PR-a, ali nikad ne kaže da li treba odobriti ili odbaciti PR — tu odluku uvek donosite vi.',
      reportTitle: 'Tehnički izveštaj',
      reportBody: 'Umesto chata, ovde dobijate detaljan pisani tehnički izveštaj o projektu i PR-u. Možete pretraživati dokument pomoću polja za pretragu na vrhu.',
      searchTitle: 'Pretraga dokumentacije',
      searchBody: 'Ukucajte pojam ovde da pronađete sva mesta gde se pominje u izveštaju — strelice vas vode kroz rezultate, a „✕" briše pretragu.',
      askQuestionTitle: 'Vaš red — postavite pitanje',
      askQuestionBody: 'Sada probajte sami: ukucajte bilo koje pitanje o ovom PR-u u polje ispod i pošaljite ga. Sačekajte pravi odgovor od AI asistenta, pa kliknite „Dalje" da nastavite.',
      switchToReportTitle: 'Prelazimo na Wiki Mode',
      switchToReportBody: 'Ova sesija je u AI Mode-u, ali u drugim sesijama možete dobiti Wiki Mode. Da biste ga videli uživo, sada ćemo privremeno prebaciti ovaj panel — umesto chata, dobijate gotov tehnički izveštaj koji možete pretraživati.',
      switchToAiTitle: 'Prelazimo na AI Mode',
      switchToAiBody: 'Ova sesija je u Wiki Mode-u, ali u drugim sesijama možete dobiti AI Mode. Da biste ga videli uživo, sada ćemo privremeno prebaciti ovaj panel — umesto izveštaja, postavljate pitanja asistentu u realnom vremenu.',
      decisionBtnTitle: 'Donošenje odluke',
      decisionBtnBody: 'Kada ste spremni, ovim dugmetom otvarate formu za finalnu odluku o Pull Requestu.',
      finishModalTitle: 'Forma za odluku — vaš red',
      finishModalBody: 'Ovo je poslednji korak. Za ovu vežbu upišite kratak komentar (npr. „Intro") i kliknite „Prihvati" ili „Odbaci" — nije bitno koje, ovo je samo vežba. To će vas automatski prebaciti na NASA-TLX upitnik, gde vas čeka sličan kratak vodič.',
    },
  },
  en: {
    finish: 'Make a decision',
    theme: { dark: 'Dark', light: 'Light' },

    loaderTitle: 'Code Review AI',
    loaderSubtitle: 'Analyze Pull Requests with AI assistance',
    repoUrlLabel: 'GitHub Repository URL',
    prNumberLabel: 'Pull Request Number',
    tokenLabel: 'GitHub Token',
    loadPr: 'Load PR',
    loading: 'Loading…',
    repoUrlError: 'Enter a valid GitHub URL (e.g. https://github.com/owner/repo)',
    prNumberError: 'Enter the PR number',
    tokenError: 'Token is required',
    invalidRepoUrl: 'Invalid repository URL. Example: https://github.com/owner/repo',
    genericError: 'An error occurred. Please try again.',

    changedFiles: 'Changed Files',
    noChangedFiles: 'No changed files.',
    statusAdded: 'added',
    statusRemoved: 'removed',
    statusModified: 'modified',

    loadingDiff: 'Loading diff…',
    diffError: 'Error loading diff.',
    diffUnavailable: 'Diff not available for this file (binary file or too many changes).',

    chatEmpty: 'Ask a question about this Pull Request using the input below or select one of the quick questions.',
    chatPlaceholder: 'Ask a question about this PR…',
    send: 'Send',
    disclaimer: '⚠️ This tool provides educational analysis. The final decision on the PR is made by the developer.',
    aiError: '_Error communicating with the AI assistant._',
    chips: [
      'Briefly explain what this PR does',
      'Are SOLID principles followed?',
      'Are there any security issues?',
      'How are test cases covered?'
    ],

    expandFileList: 'Expand file list',
    collapseFileList: 'Collapse file list',
    expandDiff: 'Expand diff viewer',
    collapseDiff: 'Collapse diff viewer',
    expandChat: 'Expand chat',
    collapseChat: 'Collapse chat',

    loadRepoContext: 'Load repository context',
    repoContextLoading: 'Loading context…',
    repoContextLoaded: 'Repository context loaded',
    repoContextError: 'Error loading repository context.',

    prDescription: 'PR Description',
    prDescriptionEmpty: 'This PR has no description.',

    modeLabel: 'Review mode',
    modeAiTitle: 'AI Mode',
    modeAiDesc: 'Ask the AI assistant questions about the PR in real time',
    modeReportTitle: 'Wiki Mode',
    modeReportDesc: 'Get a detailed written report about the PR, without chat',

    reportLoading: 'Generating detailed report for this PR…',
    reportRetry: 'Try again',
    reportError: 'Error generating the report.',
    searchPlaceholder: 'Search documentation…',
    searchNoMatches: 'No matches',

    finishModalTitle: 'Finish review',
    finishModalCommentLabel: 'Comment about this Pull Request',
    finishModalCommentPlaceholder: 'Enter your comment about this Pull Request…',
    finishModalHint: 'Enter a comment to continue.',
    finishModalAccept: 'Accept',
    finishModalReject: 'Reject',
    decisionError: 'Error saving decision. Please try again.',

    summary: 'Summary',
    showFullDescription: 'Show full description →',

    quoteToChat: '💬 Quote to chat',

    studyParticipantLabel: 'Participant ID',
    studyParticipantPlaceholder: 'e.g. 001',
    studyParticipantRequired: 'Enter your Participant ID',
    studyParticipantNotFound: 'No participant found with this ID.',
    studyAllDone: 'All sessions for this participant are finished. Thank you for participating!',
    studyLangLabel: 'Jezik / Language',
    studyLogin: 'Log in',
    studySessionLabel: 'Session',
    tour: {
      next: 'Next →',
      back: '← Back',
      skip: 'Skip tour',
      done: 'Got it, let me try it myself',
      welcomeTitle: 'Welcome to BeyondAI',
      welcomeBody: 'This short guide will walk you through every part of the app you\'ll use while reviewing a Pull Request. Click "Next" to continue, or "Skip tour" to explore on your own right away.',
      fileListTitle: 'Changed files list',
      fileListBody: 'Here you see every file this Pull Request changes. Click a file to open its changes (diff) in the middle panel.',
      summaryTitle: 'PR summary',
      summaryBody: 'A short automatic summary of the Pull Request. Clicking "Show full description" opens the full description the PR author wrote.',
      diffTitle: 'Viewing changes (diff)',
      diffBody: 'Green lines were added, red lines were removed. This is where you read exactly what changed in the file you selected.',
      quoteTitle: 'Quoting code into the chat',
      quoteBody: 'If you select part of the code with your mouse, a "Quote to chat" button appears — letting you ask the AI assistant specifically about that line or block of code.',
      chatTitle: 'Chat with the AI assistant',
      chatBody: 'Here you ask the AI assistant questions about the Pull Request — in free text, or by clicking one of the suggested quick questions. The assistant answers based on the code and PR description, but never says whether the PR should be approved or rejected — that decision is always yours.',
      reportTitle: 'Technical report',
      reportBody: 'Instead of a chat, here you get a detailed written technical report about the project and the PR. You can search the document using the search field at the top.',
      searchTitle: 'Searching the documentation',
      searchBody: 'Type a term here to find every place it\'s mentioned in the report — the arrows step through the results, and "✕" clears the search.',
      askQuestionTitle: 'Your turn — ask a question',
      askQuestionBody: 'Now try it yourself: type any question about this PR into the field below and send it. Wait for a real answer from the AI assistant, then click "Next" to continue.',
      switchToReportTitle: 'Switching to Wiki Mode',
      switchToReportBody: 'This session is in AI Mode, but in other sessions you may get Wiki Mode. To show it to you live, we\'ll now temporarily switch this panel — instead of a chat, you get a ready-made technical report you can search.',
      switchToAiTitle: 'Switching to AI Mode',
      switchToAiBody: 'This session is in Wiki Mode, but in other sessions you may get AI Mode. To show it to you live, we\'ll now temporarily switch this panel — instead of a report, you ask the assistant questions in real time.',
      decisionBtnTitle: 'Making a decision',
      decisionBtnBody: 'When you\'re ready, this button opens the form for your final decision on the Pull Request.',
      finishModalTitle: 'Decision form — your turn',
      finishModalBody: 'This is the last step. For this exercise, write a short comment (e.g. "Intro") and click "Accept" or "Reject" — either is fine, this is just practice. That will automatically take you to the NASA-TLX questionnaire, where a similar short guide is waiting for you.',
    },
  }
};
