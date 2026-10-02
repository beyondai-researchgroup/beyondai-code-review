namespace CodeReviewAI.Api.Services;

/// <summary>
/// A deliberately shorter companion to <see cref="StaticReportContent"/> — roughly half the
/// length, ~8 top-level sections instead of 18 — served only for the Intro study session
/// (<c>StudySessionId == 1</c>), never for a real Report/Hybrid session. The Report-mode static
/// doc is comprehensive by design (it's what participants actually work from); this one exists
/// purely so a first-time participant can realistically read it start to finish in a few minutes
/// during the guided tour and come away with a real sense of what the project is, not just a
/// skimmed heading. Same fictional project (closed-loop payment tokenization) and terminology as
/// the full report, so nothing here contradicts what the participant sees later in a real session.
/// </summary>
internal static class StaticIntroReportContent
{
    public static string Get(string? lang) => lang == "en" ? En : Sr;

    private const string Sr = """
        # Sistem za tokenizaciju plaćanja — kratak pregled

        Ovo je skraćen uvod u projekat koji ćete pregledati u ovoj studiji. Cilj ovog teksta nije
        da bude potpuna tehnička dokumentacija (tu ćete videti kasnije, u punoj verziji) — cilj je
        da za par minuta stekli osnovni utisak o tome šta sistem radi, koji su njegovi glavni delovi
        i zašto su napravljeni tako kako jesu. Slobodno koristite pretragu u desnom uglu da isprobate
        kako radi — ukucajte, na primer, "token" ili "novčanik" i pogledajte šta se dešava.

        ## Pregled projekta

        Sistem omogućava korisnicima da unesu novac u svoj digitalni novčanik i da ga zatim koriste
        za plaćanje unutar zatvorenog kruga prodavaca koji učestvuju u programu — zamislite ga kao
        interni sistem lojalnosti/plaćanja jednog trgovinskog lanca ili platforme, ne kao opštu
        kriptovalutu. Novac koji korisnik unese se pretvara u interne "tokene" koji se prenose
        između novčanika prilikom svake kupovine, a sistem u pozadini vodi tačnu, proverljivu
        evidenciju svake transakcije.

        ## Motivacija

        Klasični sistemi plaćanja (kartice, bankovni transferi) imaju proviziju i kašnjenje pri
        svakoj transakciji, što je posebno neisplativo za sitna, česta plaćanja unutar jedne
        platforme. Zatvoren sistem tokenizacije rešava to tako što se stvarni novac menja samo
        jednom (kada korisnik "puni" novčanik), a sve dalje transakcije unutar platforme su skoro
        trenutne i bez dodatne provizije, jer se svode na ažuriranje internih evidencija, a ne na
        stvarni bankovni transfer za svaku kupovinu ponaosob.

        ## Ključni koncepti

        Nekoliko pojmova će se stalno pojavljivati u kodu i dokumentaciji: **novčanik (wallet)** je
        korisnički nalog koji drži balans tokena; **token** je najmanja jedinica vrednosti u sistemu,
        uvek vezana za tačno jedan novčanik u datom trenutku; **transakcija** je prenos tokena sa
        jednog novčanika na drugi, zapisan tako da se kasnije može nezavisno proveriti da nije
        izmenjen (koristi se heš-lanac sličan onom u blokčejn sistemima, iako ovo nije javni
        blokčejn). Sistem je izgrađen kao skup manjih, nezavisnih mikroservisa, a ne kao jedna
        velika aplikacija — svaki mikroservis je zadužen za jednu jasno određenu odgovornost
        (novčanici, transakcije, tokeni, itd.).

        ## Funkcionalni zahtevi

        Sistem, u osnovi, treba da omogući: registraciju korisnika i otvaranje novčanika; punjenje
        novčanika stvarnim novcem preko spoljnog platnog provajdera; slanje tokena drugom novčaniku
        (plaćanje); pregled istorije transakcija za dati novčanik; i osnovnu administraciju od strane
        operatera platforme (npr. privremeno zamrzavanje novčanika u slučaju sumnje na zloupotrebu).
        Svaka transakcija mora biti atomarna — ili se u potpunosti izvrši, ili se uopšte ne izvrši,
        nikad napola.

        ## Arhitektura sistema

        Sistem je podeljen na nekoliko mikroservisa koji komuniciraju preko internog API gejtveja:
        servis za novčanike (drži balanse i brine o zaključavanju tokom transakcije), servis za
        transakcije (koordiniše sam prenos i vodi evidenciju), servis za tokene (kreira/uništava
        tokene pri punjenju/isplati) i gejtvej servis (jedina tačka koju spoljni klijenti direktno
        vide, prosleđuje pozive odgovarajućem internom servisu). Svaki mikroservis ima svoju bazu
        podataka — namerno se izbegava deljena baza između servisa, da bi ostali nezavisni jedan od
        drugog.

        ## Tok izvršavanja transakcije

        Kad korisnik A plaća korisniku B: (1) gejtvej primi zahtev i prosledi ga servisu za
        transakcije; (2) servis za transakcije zatraži od servisa za novčanike da privremeno
        zaključa potreban iznos kod korisnika A; (3) ako zaključavanje uspe, transakcija se upisuje
        u evidenciju sa statusom "u toku"; (4) tokeni se stvarno prenose — balans A se umanjuje,
        balans B se uvećava, oba u istoj bazi transakcija radi konzistentnosti; (5) transakcija se
        označava kao "završena", zaključavanje se skida. Ako bilo koji korak (2)-(4) ne uspe, ceo
        proces se poništava i transakcija se označava kao "neuspešna" — korisnik A nikad ne ostaje
        sa privremeno zaključanim, a zapravo izgubljenim, novcem.

        ## Bezbednosna razmatranja

        Pošto sistem direktno rukuje novcem korisnika, bezbednost je centralna briga na svakom
        nivou: sva komunikacija između servisa je enkriptovana; svaki upis u evidenciju transakcija
        sadrži heš prethodnog upisa (isti princip kao kod blokčejna), tako da bi svaka naknadna
        izmena istorije bila odmah uočljiva; pristup novčaniku zahteva autentifikaciju korisnika pri
        svakoj transakciji, ne samo pri prijavi; a operater platforme ima ograničena administratorska
        prava (može zamrznuti novčanik, ali ne može sam sebi dodeliti tokene).

        ## Model podataka

        Tri glavne tabele nose najveći deo logike: **Wallet** (novčanik — vlasnik, trenutni balans,
        status), **Token** (pojedinačni token — jedinstveni identifikator, novčanik kome trenutno
        pripada) i **Transaction** (transakcija — pošiljalac, primalac, iznos, vreme, status, heš
        prethodne transakcije). U punoj dokumentaciji (koju ćete videti kasnije) postoje i dodatne
        tabele i detaljniji opis svake kolone.
        """;

    private const string En = """
        # Payment Tokenization System — Quick Overview

        This is a shortened introduction to the project you'll be reviewing in this study. The goal
        here isn't to be complete technical documentation (you'll see the full version later) — it's
        to give you a real sense, in a few minutes, of what the system does, what its main parts
        are, and why they're built the way they are. Feel free to try the search box in the top
        corner — type something like "token" or "wallet" and see what happens.

        ## Project Overview

        The system lets users load money into a digital wallet and then spend it within a closed
        circle of participating merchants — think of it as an internal loyalty/payment system for
        one retail chain or platform, not a general-purpose cryptocurrency. Money a user loads is
        converted into internal "tokens" that move between wallets with every purchase, while the
        system keeps an accurate, verifiable record of every transaction behind the scenes.

        ## Motivation

        Classic payment rails (cards, bank transfers) charge a fee and add latency to every single
        transaction, which is especially wasteful for small, frequent payments within one platform.
        A closed tokenization system solves this by exchanging real money only once (when a user
        "tops up" their wallet) — every later transaction within the platform is near-instant and
        fee-free, since it's just an internal ledger update, not an actual bank transfer for every
        individual purchase.

        ## Key Concepts

        A handful of terms come up constantly in the code and docs: a **wallet** is a user account
        holding a token balance; a **token** is the smallest unit of value in the system, always
        tied to exactly one wallet at any given moment; a **transaction** is a transfer of tokens
        from one wallet to another, recorded so it can later be independently verified as unaltered
        (a hash chain similar to blockchain systems, though this isn't a public blockchain). The
        system is built as a set of small, independent microservices rather than one large
        application — each microservice owns one clearly scoped responsibility (wallets,
        transactions, tokens, etc.).

        ## Functional Requirements

        At its core, the system needs to support: user registration and wallet creation; topping up
        a wallet with real money via an external payment provider; sending tokens to another wallet
        (a payment); viewing transaction history for a given wallet; and basic administration by
        platform operators (e.g. temporarily freezing a wallet on suspected abuse). Every transaction
        must be atomic — it either fully completes or doesn't happen at all, never halfway.

        ## System Architecture

        The system is split into several microservices communicating through an internal API
        gateway: the wallet service (holds balances, handles locking during a transaction), the
        transaction service (coordinates the actual transfer and keeps the record), the token
        service (creates/destroys tokens on top-up/withdrawal), and the gateway service (the only
        thing external clients ever see directly, forwarding calls to the right internal service).
        Each microservice owns its own database — a shared database between services is deliberately
        avoided, to keep them genuinely independent of each other.

        ## Transaction Execution Flow

        When user A pays user B: (1) the gateway receives the request and forwards it to the
        transaction service; (2) the transaction service asks the wallet service to temporarily lock
        the needed amount on A's wallet; (3) if the lock succeeds, the transaction is recorded with
        an "in progress" status; (4) the tokens actually move — A's balance decreases, B's balance
        increases, both within the same transaction for consistency; (5) the transaction is marked
        "completed" and the lock is released. If any step (2)-(4) fails, the whole process rolls
        back and the transaction is marked "failed" — user A is never left with money that's
        temporarily locked but effectively lost.

        ## Security Considerations

        Since the system directly handles users' money, security is a central concern at every
        level: all inter-service communication is encrypted; every entry in the transaction record
        includes a hash of the previous entry (the same principle blockchains use), so any later
        tampering with the history would be immediately detectable; accessing a wallet requires user
        authentication on every transaction, not just at login; and platform operators have limited
        administrative rights (they can freeze a wallet, but can't grant themselves tokens).

        ## Data Model

        Three main tables carry most of the logic: **Wallet** (owner, current balance, status),
        **Token** (a single token — unique identifier, the wallet it currently belongs to), and
        **Transaction** (sender, recipient, amount, timestamp, status, hash of the previous
        transaction). The full documentation (which you'll see later) has additional tables and a
        more detailed description of every column.
        """;
}
