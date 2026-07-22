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
  studyChooseMode: string;
  studyStart: string;
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
    modeReportTitle: 'Report Mode',
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
    studyChooseMode: 'Izaberite način pregleda za uvodnu sesiju',
    studyStart: 'Započni sesiju',
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
    modeReportTitle: 'Report Mode',
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
    studyChooseMode: 'Choose the review mode for the intro session',
    studyStart: 'Start session',
  }
};
