namespace CodeReviewAI.Api.Services;

/// <summary>
/// Predefined Report Mode content used for demo/test environments, where the report panel
/// should show a fixed, well-formatted technical document instead of an AI-generated report.
/// Toggle off via <c>Session:UseStaticReport</c> in configuration to restore live AI generation
/// (the dynamic generation code path in <see cref="ReviewSessionEndpoints"/> is left untouched).
/// </summary>
internal static class StaticReportContent
{
    /// <summary>Returns the predefined report text for the given UI language.</summary>
    public static string Get(string lang) => lang == "en" ? En : Sr;

    private const string Sr = """
        # Tehnička dokumentacija — Sistem za tokenizaciju zatvorenog platnog sistema

        ## Pregled projekta
        Ovaj projekat implementira tokenizaciju digitalnih novčanika u okviru zatvorenog platnog
        sistema, korišćenjem blokčejn tehnologije, digitalnih novčanika i tokena. Cilj je da se
        poveća bezbednost transakcija i zaštita korisničkih podataka, uz očuvanje prednosti koje
        zatvoreni sistemi plaćanja inače nude — nižih transakcionih troškova, veće lojalnosti
        korisnika i bolje kontrole nad podacima.

        Sistem je realizovan kroz mikroservisnu arhitekturu u C# / .NET okruženju, sa četiri
        samostalna mikroservisa (WalletMS, TokenMS, TransactionMS, BlockchainMS) iza zajedničkog
        API Gateway-a.

        ## Motivacija
        Zatvoreni sistemi plaćanja (npr. interni novčanici kompanija, bonus/lojalti programi)
        tradicionalno nude niže transakcione troškove i bolju kontrolu podataka u poređenju sa
        otvorenim sistemima (Visa, MasterCard), ali po cenu manje fleksibilnosti i ograničene
        upotrebljivosti van sopstvenog ekosistema. Glavni rizik ostaje bezbednost: centralizovano
        čuvanje stanja novčanika je osetljivo na povrede podataka, dok nepostojanje nezavisno
        verifikovanog dokaza o transakcijama otežava reviziju i izgradnju poverenja.

        Tokenizacija u kombinaciji sa upisom dokaza o transakcijama na javni blokčejn rešava oba
        problema: korisnička sredstva se predstavljaju kao tokeni čiji integritet se može
        kriptografski verifikovati, a sažetak svih transakcija u određenom periodu (Merkelov
        koren) upisuje se u nepromenljiv, javno dostupan registar.

        ## Ključni koncepti

        ### Otvoreni i zatvoreni sistemi plaćanja
        - **Otvoreni sistemi** (four-party model) uključuju banku platitelja, banku primaoca i
          centralizovanu treću stranu (npr. Visa/MasterCard); fleksibilniji su, ali skuplji po
          transakciji.
        - **Zatvoreni sistemi** (three-party model) funkcionišu unutar jedne kompanije —
          transakcije se autorizuju i obrađuju na istoj platformi, što omogućava niže i
          predvidljive troškove.

        ### Digitalni novčanici i tokeni
        Novčanici se dele na otvorene, poluzatvorene i zatvorene, u zavisnosti od toga da li
        omogućavaju isplatu na bilo koji račun, transakcije samo unutar partnerske mreže, ili
        isključivo unutar jedne kompanije. Tokeni predstavljaju digitalna sredstva — u ovom
        sistemu svaki token nosi sopstvenu heš-potpisanu strukturu podataka (stanje, tip,
        verziju, vremensku oznaku, ID transakcije) koja se može nezavisno validirati.

        ### Blokčejn, heš funkcije i Merkelovo stablo
        Blokčejn obezbeđuje nepromenljiv, decentralizovan registar — svaki upis je zaštićen
        kriptografskim heširanjem prethodnog stanja. Sistem koristi SHA-256 za heširanje
        transakcija i **Merkelovo stablo** za efikasno agregiranje velikog broja transakcija u
        jedan koren (root), čime se obezbeđuje da bilo kakva izmena bilo koje transakcije u skupu
        promeni vrednost korena i odmah bude detektovana.

        ### Mikroservisna arhitektura i C4 model
        Arhitektura je modelovana C4 pristupom (Kontekst → Kontejneri → Komponente → Kod), gde je
        sistem podeljen na nezavisne mikroservise koji komuniciraju putem JSON/HTTP poziva.

        ## Funkcionalni zahtevi

        | Oznaka | Opis |
        |---|---|
        | Z1 | Autentifikacija i autorizacija korisnika |
        | Z2 | Kreiranje i upravljanje digitalnim novčanicima |
        | Z3 | Kreiranje i upravljanje tokenima |
        | Z4 | Verzionisanje tokena i podrška za više verzija istovremeno |
        | Z5 | Validacija tokena |
        | Z6 | Kreiranje transakcija (deponovanje, plaćanje, skidanje sredstava) |
        | Z7 | Validacija transakcija |
        | Z8 | Upis dokaza o izvršenim transakcijama u blokčejn |

        ## Nefunkcionalni zahtevi

        | Oznaka | Opis |
        |---|---|
        | NF1 | Sistem mora obraditi najmanje 500 transakcija u minuti bez degradacije vremena odziva iznad 300ms (p95) |
        | NF2 | Podaci o stanju novčanika i tokenima moraju biti šifrovani u mirovanju (at rest) i u prenosu (TLS 1.2+) |
        | NF3 | Mapiranje korisnik → adresa novčanika mora biti fizički odvojeno od podataka o stanju tokena |
        | NF4 | Svaki mikroservis mora biti nezavisno deployabilan i horizontalno skalabilan |
        | NF5 | Sistem mora ostati dostupan (uz degradiranu funkcionalnost) i kada je BlockchainMS privremeno nedostupan — upisi na blokčejn se stavljaju u red čekanja umesto da blokiraju transakciju |
        | NF6 | Svaki pokušaj validacije tokena i transakcije se loguje radi revizije (audit log), bez čuvanja osetljivih podataka u čistom tekstu |
        | NF7 | Vreme oporavka sistema (RTO) posle pada pojedinačnog mikroservisa ne sme preći 2 minuta |

        ## Arhitektura sistema

        ### Kontekst
        Dve korisničke uloge komuniciraju sa sistemom: **zaposleni** (izvršava plaćanja) i
        **administrator** (dodatno upravlja deponovanjem i skidanjem sredstava). Sistem se
        oslanja na eksterni servis za autentifikaciju (SSO) i komunicira sa javnim blokčejnom
        radi upisa dokaza o transakcijama.

        ### Kontejneri

        | Kontejner | Opis | Tehnologije |
        |---|---|---|
        | API Gateway | Ulazna tačka sistema — autentifikacija, autorizacija, rutiranje, rate limiting | ASP.NET, Yarp |
        | WalletMS | Kreiranje i upravljanje digitalnim novčanicima i tokenima korisnika | ASP.NET, EF Core, Redis, Azure Key Vault |
        | TokenMS | Kreiranje, validacija i verzionisanje tokena | ASP.NET |
        | TransactionMS | Kreiranje transakcija, generisanje Merkelovog stabla | ASP.NET, EF Core |
        | BlockchainMS | Upis dokaza o transakcijama u javni blokčejn | ASP.NET, Nethereum |
        | WalletDb / TransactionBankDb | Perzistencija novčanika i transakcija | SQL Server / SQLite |

        Sve što se nalazi iza API Gateway-a je izolovano u privatnoj mreži.

        ### Komponente po mikroservisu

        **WalletMS** — `Wallet Controller` izlaže pristupne tačke ka API Gateway-u, TransactionMS
        i TokenMS, i koordinira zaključavanje novčanika tokom transakcije putem `Redis Service`-a.
        `Wallet Service` upravlja CRUD operacijama nad novčanicima (preko Entity Framework
        Core-a), dok `Azure Key Vault Service` bezbedno čuva mapiranje korisnik → adresa
        novčanika, odvojeno od podataka o stanju tokena. `Token Service` dobavlja i ažurira
        tokene vezane za određeni novčanik.

        **TokenMS** — bezstanjski (stateless) servis, lako skalabilan. `Token Service`
        implementira **Factory Method** obrazac (`IToken` interfejs, `TokenFactory` klasa) za
        kreiranje i ažuriranje tokena različitih verzija, čime se nove verzije tokena mogu
        uvoditi bez izmene postojećeg koda.

        **TransactionMS** — `Transaction Controller` prima zahteve za plaćanje, deponovanje i
        skidanje sredstava. Inicirane transakcije se postavljaju u memorijski **bazen
        transakcija** (transaction pool); pozadinski `Transaction Hosted Service` periodično
        (svakih 10 sekundi u trenutnoj konfiguraciji) uzima sve transakcije iz bazena, generiše
        Merkelov koren i validacione putanje, te prosleđuje koren ka BlockchainMS-u.

        **BlockchainMS** — `Blockchain Service` upisuje Merkelov koren u pametni ugovor na
        Ethereum mreži (preko Nethereum biblioteke) i vraća heš blokčejn transakcije kao dokaz o
        upisu, koji se zatim perzistuje uz odgovarajuću bankovnu transakciju.

        ## Tok izvršavanja transakcije

        Tipičan tok plaćanja između dva korisnika unutar sistema odvija se kroz sledeće korake:

        1. **Zahtev za plaćanje** — Zaposleni šalje zahtev za plaćanje kroz API Gateway, koji
           proverava autentifikaciju (SSO token) i prosleđuje zahtev ka TransactionMS.
        2. **Zaključavanje novčanika** — TransactionMS poziva WalletMS da zaključa novčanike i
           platioca i primaoca preko Redis Service-a, sprečavajući da se isti novčanik koristi u
           dve konkurentne transakcije.
        3. **Validacija tokena** — WalletMS dobavlja trenutne tokene oba novčanika i poziva
           TokenMS da validira njihov integritet (`IsTokenValid()`); ako validacija ne prođe,
           transakcija se odmah odbija.
        4. **Kreiranje transakcije** — TransactionMS kreira zapis transakcije (status `Pending`)
           i postavlja je u memorijski bazen transakcija, uz snimak stanja tokena pre transakcije
           (`TokenFromOld` / `TokenToOld`).
        5. **Ažuriranje stanja tokena** — TokenMS generiše nove tokene sa ažuriranim stanjem (nova
           heš-potpisana struktura) i vraća ih WalletMS-u, koji ih perzistuje i oslobađa
           zaključavanje novčanika.
        6. **Periodična agregacija** — `Transaction Hosted Service` u TransactionMS-u svakih 10
           sekundi uzima sve transakcije iz bazena od poslednjeg ciklusa, generiše Merkelov koren
           (`GenerateMerkleRoot`) i za svaku transakciju računa validacionu putanju
           (`GetMerklePath`).
        7. **Upis na blokčejn** — TransactionMS prosleđuje Merkelov koren ka BlockchainMS-u, koji
           poziva pametni ugovor (`set`) na Ethereum mreži i vraća heš blokčejn transakcije.
        8. **Zatvaranje transakcije** — TransactionMS ažurira status transakcije u `Success`,
           upisuje `EtheriumProof` (heš blokčejn transakcije) i validacionu putanju uz
           odgovarajući token, čime transakcija postaje nezavisno proverljiva.

        Ako bilo koji korak od 1 do 5 ne uspe, transakcija se označava statusom `Failed`, zaključani
        novčanici se odmah oslobađaju, a token se ne menja — sistem ostaje u konzistentnom stanju
        pre pokušaja transakcije.

        ## Ključni implementacioni mehanizmi

        - **Heš-potpisana struktura tokena** — svako polje tokena (stanje, tip, vreme kreiranja,
          ID transakcije...) ima pridruženu heš vrednost; `IsTokenValid()` ponovo izračunava sve
          heševe i poredi ih sa sačuvanim vrednostima, čime se otkriva svaka neovlašćena izmena.
        - **Zaključavanje novčanika** — Redis se koristi za privremeno zaključavanje novčanika
          koji učestvuju u transakciji, sprečavajući dvostruko trošenje (double spending) usled
          konkurentnih zahteva.
        - **Merkelovo stablo** — `GenerateMerkleRoot` i `GetMerklePath` (SHA-256) agregiraju
          transakcije iz bazena u jedan koren i generišu validacionu putanju za svaku pojedinačnu
          transakciju, koja se čuva uz token kao dokaz uključenosti u upisani koren.
        - **Pametni ugovor** — minimalni Solidity ugovor (`set` / `get` nad jednim string poljem)
          prima Merkelov koren i upisuje ga na Ethereum test mrežu, čime nastaje javno
          verifikovan dokaz o izvršenim transakcijama.

        ## Bezbednosna razmatranja

        Pored heš-potpisane strukture tokena i zaključavanja novčanika opisanih iznad, sistem
        primenjuje dodatne mere:

        - **Odvajanje identiteta od stanja** — mapiranje korisnik → adresa novčanika čuva se
          isključivo u Azure Key Vault-u, dok WalletDb i TransactionBankDb sadrže samo adrese i
          tokene, bez direktne veze ka identitetu korisnika. Kompromitovanje jedne baze samo po
          sebi ne otkriva čiji su novčanici u pitanju.
        - **Nezavisna verifikacija van sistema** — pošto se Merkelov koren upisuje na javni
          Ethereum, svaka zainteresovana strana (uključujući spoljne revizore) može nezavisno
          proveriti da li je određena transakcija zaista uključena u upisani koren, bez pristupa
          internim bazama sistema.
        - **Rate limiting na API Gateway-u** — sprečava zloupotrebu putem masovnog slanja zahteva
          (npr. pokušaj brute-force validacije tokena).
        - **Princip najmanjih privilegija** — administratorska uloga je odvojena od uloge
          zaposlenog i ima pristup isključivo funkcijama deponovanja/skidanja sredstava; nijedna
          uloga nema direktan pristup bazama podataka.
        - **Poznati preostali rizici** — sistem trenutno ne štiti od kompromitovanog Azure Key
          Vault naloga (jedina tačka koja povezuje identitet i novčanik) i ne implementira
          rotaciju ključeva korišćenih za heširanje; oba su identifikovana kao pravci daljeg
          unapređenja bezbednosti.

        ## Model podataka

        Perzistencija je podeljena na dve baze: `WalletDb` (novčanici i tokeni) i
        `TransactionBankDb` (transakcije). Token i transakcija čuvaju heš-potpise svojih
        ključnih polja, što omogućava naknadnu validaciju integriteta bez ponovnog pristupa
        originalnom izvoru podataka. Mapiranje korisnik → adresa novčanika čuva se odvojeno, u
        Azure Key Vault-u — ni jedna od dve baze ne sadrži tu vezu.

        ### Tabela: Wallet

        | Kolona | Tip | Opis |
        |---|---|---|
        | Address | GUID (PK) | Jedinstvena adresa novčanika |
        | Tokens | navigacija (1:N) | Tokeni trenutno vezani za ovaj novčanik |

        ### Tabela: Token

        | Kolona | Tip | Opis |
        |---|---|---|
        | TokenId | GUID (PK) | Jedinstveni identifikator tokena |
        | WalletAddress | GUID (FK → Wallet) | Novčanik kojem token pripada |
        | TokenData | JSON (string) | Serijalizovana struktura — stanje, tip, verzija, vreme kreiranja, ID transakcije, dokaz uključenosti u Merkelovo stablo |
        | TokenDataSignature | int | Heš potpis čitavog TokenData bloka, koristi se za detekciju neovlašćene izmene |

        ### Tabela: Transaction

        | Kolona | Tip | Opis |
        |---|---|---|
        | TransactionId | GUID (PK) | Jedinstveni identifikator transakcije |
        | TransactionType | enum | `Payment` / `Deposit` / `Withdraw` |
        | TokenFromOld / TokenToOld | JSON (string) | Snimak stanja uključenih tokena pre transakcije (audit trag) |
        | TokenType | enum | Tip digitalne valute koju transakcija pomera |
        | Amount | decimal | Iznos transakcije |
        | FeeFrom / FeeTo | decimal | Transakcione takse po strani |
        | TransactionStatus | enum | `Pending` / `Success` / `Failed` |
        | TransactionIniciator | string | Identifikator korisnika koji je inicirao transakciju |
        | TransactionSignature | string (heš) | SHA-256 heš transakcije — list u Merkelovom stablu |
        | EtheriumProof | string | Heš blokčejn transakcije kojom je upisan koren koji uključuje ovu transakciju |

        ### Enumeracije

        | Enum | Vrednosti | Opis |
        |---|---|---|
        | TransactionType | `Payment`, `Deposit`, `Withdraw` | Tip operacije koju transakcija predstavlja |
        | TransactionStatus | `Pending`, `Success`, `Failed` | Trenutno stanje obrade transakcije |
        | TokenType | `Standard`, `Bonus`, `Promotional` | Standardna sredstva, lojalti bonus poeni, ili promotivni krediti |

        ## Ključni isečci koda

        ### Interfejs za verzionisanje tokena

        ```csharp
        public interface IToken
        {
            bool IsTokenValid();
            TokenDto ToTokenDto();
        }
        ```

        Svaka verzija tokena (`TokenV1`, eventualno `TokenV2` u budućnosti) implementira ovaj
        interfejs. `TokenFactory` na osnovu polja `Version` iz `TokenData` bira tačnu
        implementaciju — to je srž Factory Method obrasca opisanog u prethodnoj sekciji.

        ### Heš-potpis tokena (skraćeno)

        ```csharp
        private void CalculateHashValues()
        {
            TokenData.BalanceHash = HashFunction(TokenData.Balance.ToByteArray());
            TokenData.VersionHash = HashFunction(TokenData.Version.ToByteArray());
            TokenData.CreatedHash = HashFunction(TokenData.Created.ToByteArray());
            TokenIdHash           = HashFunction(TokenId.ToByteArray());
            WalletAddressHash     = HashFunction(WalletAddress.ToByteArray());
            TokenDataSignature    =
                HashFunction(CreateIntArrayForSignatureHashCalculation().ToByteArray());
        }
        ```

        `IsTokenValid()` ponovo izvršava isti proračun i poredi rezultat sa sačuvanim heš
        vrednostima — ako se ijedno polje promenilo van ove putanje, validacija odmah pada.

        ### Generisanje Merkelovog korena (skraćeno)

        ```csharp
        public static string GenerateMerkleRoot(List<string> transactions)
        {
            var level = transactions.Select(HashFunc).ToList();
            while (level.Count > 1)
            {
                var next = new List<string>();
                for (int i = 0; i < level.Count; i += 2)
                    next.Add(i + 1 < level.Count
                        ? HashFunc(level[i] + level[i + 1])
                        : level[i]);
                level = next;
            }
            return level[0];
        }
        ```

        Funkcija agregira heš vrednosti svih transakcija u trenutnom bazenu u jedan koren —
        upravo ta vrednost se upisuje u pametni ugovor na blokčejnu.

        ### Pametni ugovor (Solidity)

        ```solidity
        // SPDX-License-Identifier: MIT
        pragma solidity ^0.8.0;

        contract SimpleStorage {
            string public storedData;

            function set(string memory x) public {
                storedData = x;
            }

            function get() public view returns (string memory) {
                return storedData;
            }
        }
        ```

        Minimalan ugovor sa jednim string poljem — `set` upisuje Merkelov koren, `get` ga čita
        radi nezavisne provere sa bilo kog Ethereum klijenta ili explorer alata.

        ### Fabrika tokena (Factory Method)

        ```csharp
        public static class TokenFactory
        {
            public static IToken Create(TokenDataDto data) => data.Version switch
            {
                1 => new TokenV1(data),
                _ => throw new NotSupportedException($"Nepodržana verzija tokena: {data.Version}")
            };
        }
        ```

        Ova statička fabrika je jedino mesto u sistemu koje zna za konkretne implementacije
        `IToken`-a; dodavanje `TokenV2` svodi se na novi `case` u `switch` izrazu, bez izmene
        koda koji tokene koristi.

        ### Generisanje Merkelove validacione putanje (skraćeno)

        ```csharp
        public static List<string> GetMerklePath(List<string> hashes, int index)
        {
            var path = new List<string>();
            var level = hashes;
            while (level.Count > 1)
            {
                var isRightNode = index % 2 == 1;
                var pairIndex = isRightNode ? index - 1 : index + 1;
                if (pairIndex < level.Count)
                    path.Add(level[pairIndex]);

                var next = new List<string>();
                for (int i = 0; i < level.Count; i += 2)
                    next.Add(i + 1 < level.Count ? HashFunc(level[i] + level[i + 1]) : level[i]);

                level = next;
                index /= 2;
            }
            return path;
        }
        ```

        Putanja se čuva uz token i omogućava da bilo ko nezavisno rekonstruiše Merkelov koren
        polazeći samo od heša jedne transakcije i ove putanje, bez pristupa svim ostalim
        transakcijama u bazenu.

        ### Zaključavanje novčanika (Redis)

        ```csharp
        public async Task<IAsyncDisposable> LockWalletAsync(Guid walletAddress, TimeSpan timeout)
        {
            var key = $"wallet-lock:{walletAddress}";
            var token = Guid.NewGuid().ToString();
            var acquired = await _redis.StringSetAsync(key, token, timeout, When.NotExists);

            if (!acquired)
                throw new WalletLockedException(walletAddress);

            return new RedisLockHandle(_redis, key, token);
        }
        ```

        Zaključavanje je implementirano kao Redis `SET ... NX` sa istekom roka (timeout), čime se
        izbegava trajno zaključan novčanik u slučaju da servis koji drži zaključavanje neočekivano
        padne.

        ### Rutiranje na API Gateway-u (YARP konfiguracija)

        ```json
        {
          "ReverseProxy": {
            "Routes": {
              "wallet-route": {
                "ClusterId": "wallet-cluster",
                "Match": { "Path": "/api/wallets/{**catch-all}" }
              },
              "transaction-route": {
                "ClusterId": "transaction-cluster",
                "Match": { "Path": "/api/transactions/{**catch-all}" }
              }
            },
            "Clusters": {
              "wallet-cluster": {
                "Destinations": { "d1": { "Address": "http://walletms:8080" } }
              },
              "transaction-cluster": {
                "Destinations": { "d1": { "Address": "http://transactionms:8080" } }
              }
            }
          }
        }
        ```

        API Gateway koristi YARP (Yet Another Reverse Proxy) za rutiranje na osnovu prefiksa
        putanje ka odgovarajućem mikroservisu; svaki klaster može imati više odredišta radi
        horizontalnog skaliranja.

        ## Pregled API-ja

        Svi javno izloženi API-ji prolaze kroz API Gateway; interni pozivi između mikroservisa se
        ne izlažu spolja.

        ### WalletMS

        | Metod | Putanja | Opis |
        |---|---|---|
        | GET | `/api/wallets/{address}` | Vraća osnovne podatke o novčaniku i listu vezanih tokena |
        | POST | `/api/wallets` | Kreira novi novčanik za trenutno autentifikovanog korisnika |
        | POST | `/api/wallets/{address}/lock` | Interni poziv — zaključava novčanik za trajanje transakcije |
        | DELETE | `/api/wallets/{address}/lock` | Interni poziv — oslobađa zaključavanje novčanika |

        ### TokenMS

        | Metod | Putanja | Opis |
        |---|---|---|
        | POST | `/api/tokens/validate` | Validira integritet prosleđenog tokena (`IsTokenValid`) |
        | POST | `/api/tokens` | Kreira novi token date verzije preko `TokenFactory` |
        | PUT | `/api/tokens/{tokenId}` | Ažurira stanje postojećeg tokena nakon transakcije |

        ### TransactionMS

        | Metod | Putanja | Opis |
        |---|---|---|
        | POST | `/api/transactions/payment` | Inicira plaćanje između dva novčanika |
        | POST | `/api/transactions/deposit` | Inicira deponovanje sredstava (samo administrator) |
        | POST | `/api/transactions/withdraw` | Inicira skidanje sredstava (samo administrator) |
        | GET | `/api/transactions/{id}` | Vraća status i detalje pojedinačne transakcije, uključujući `EtheriumProof` kada je dostupan |

        ### BlockchainMS

        | Metod | Putanja | Opis |
        |---|---|---|
        | POST | `/api/blockchain/proofs` | Interni poziv — upisuje prosleđeni Merkelov koren na blokčejn i vraća heš transakcije |
        | GET | `/api/blockchain/proofs/{hash}` | Vraća status potvrde (broj konfirmacija) za dati heš blokčejn transakcije |

        ## Evaluacija
        Sistem je testiran upoređivanjem stanja novčanika nakon identičnog niza transakcija
        (deponovanje, plaćanje, skidanje sredstava) sa postojećim, netokenizovanim sistemom
        zatvorenog plaćanja. Stanja sredstava korisnika bila su identična u oba sistema, što
        potvrđuje tačnost implementirane logike. Dodatno, dokaz o transakcijama je uspešno upisan
        i pronađen na blokčejnu, a vrednost upisanog dokaza poklapa se sa zapisanom transakcijom u
        bazi.

        U pogledu performansi, postojeći netokenizovani sistem trenutno radi brže za mali broj
        transakcija, ali se očekuje da implementirano rešenje bolje skalira sa porastom obima
        transakcija, zahvaljujući bezstanjskim mikroservisima (TokenMS, BlockchainMS) i
        agregaciji transakcija putem Merkelovog stabla.

        ## Strategija testiranja

        - **Jedinični testovi** pokrivaju heš-potpisivanje i validaciju tokena
          (`CalculateHashValues`, `IsTokenValid`), generisanje Merkelovog korena i validacione
          putanje (`GenerateMerkleRoot`, `GetMerklePath`), kao i biranje implementacije u
          `TokenFactory` po verziji.
        - **Integracioni testovi** proveravaju ceo tok transakcije unutar TransactionMS-a i
          WalletMS-a, uz test-double implementaciju BlockchainMS-a (bez stvarnog upisa na Ethereum
          test mrežu), kako bi testovi ostali brzi i deterministički.
        - **End-to-end scenariji** izvršavaju se povremeno na Ethereum test mreži (testnet), radi
          provere da pametni ugovor ispravno prima i vraća upisane vrednosti u realnim uslovima
          mrežnog kašnjenja.
        - **Poređenje sa referentnim sistemom** — kao što je opisano u sekciji Evaluacija, stanja
          novčanika se automatski upoređuju sa rezultatima postojećeg netokenizovanog sistema nad
          istim ulaznim podacima, radi regresione provere tačnosti.

        ## Ograničenja
        - Sistem zavisi od eksternog Active Directory-ja za autentifikaciju — nedostupnost AD-a
          čini ceo sistem nedostupnim.
        - Deponovanje sredstava trenutno zahteva fizički kontakt sa administratorom, što
          ograničava skalabilnost.
        - `GenerateMerkleRoot` ne ograničava broj transakcija po stablu — pri visokoj frekvenciji
          transakcija to može uticati na performanse.
        - Upis na Ethereum nije besplatan — cena gasa varira i mora se uzeti u obzir prilikom
          određivanja transakcionih taksi.
        - WalletMS i TransactionMS su stanjski (stateful) servisi, pa njihovo skaliranje zahteva
          napredniju koordinaciju u odnosu na bezstanjske TokenMS i BlockchainMS.

        ## Prednosti
        - Povećana bezbednost transakcija i podataka korisnika kroz kombinaciju tokenizacije,
          zaključavanja novčanika i upisa dokaza na blokčejn.
        - Niže i proporcionalne transakcione takse u odnosu na otvorene sisteme plaćanja.
        - Centralizovana kontrola pristupa kroz Active Directory integraciju (uloge, politike
          lozinki, višefaktorska autentifikacija).
        - Potpuna nezavisnost od spoljašnjih platnih provajdera i njihovih regulativa.

        ## Rečnik pojmova

        | Pojam | Objašnjenje |
        |---|---|
        | Zatvoreni sistem plaćanja | Platni sistem u kome se sve transakcije obrađuju unutar jedne organizacije, bez posrednika treće strane |
        | Tokenizacija | Zamena osetljivih podataka (stanja sredstava) generisanim identifikatorom (tokenom) čiji se integritet može nezavisno proveriti |
        | Merkelovo stablo | Binarno stablo heš vrednosti koje omogućava efikasnu i sigurnu verifikaciju velikog skupa podataka preko jednog korena |
        | Merkelova validaciona putanja | Niz heš vrednosti potreban da se od heša jedne transakcije rekonstruiše Merkelov koren, bez potrebe za svim ostalim transakcijama |
        | Dvostruko trošenje (double spending) | Situacija u kojoj se isto sredstvo potroši više puta usled konkurentnih transakcija — sprečava se zaključavanjem novčanika |
        | Pametni ugovor (smart contract) | Program koji se izvršava na blokčejnu i čije se izvršenje i rezultat mogu nezavisno proveriti |
        | Gas (Ethereum) | Naknada koja se plaća za izvršavanje operacija na Ethereum mreži, uključujući upis podataka u pametni ugovor |
        | Bazen transakcija (transaction pool) | Privremeno memorijsko skladište transakcija koje čekaju da budu agregirane u Merkelov koren i upisane na blokčejn |

        ## Plan daljeg razvoja
        - Upis dokaza o transakcijama na više javnih blokčejnova istovremeno, radi dodatne
          otpornosti.
        - Zaključavanje samo onog tipa tokena koji učestvuje u transakciji, umesto celog
          novčanika.
        - Integracija otvorenog bankarstva za automatizovano deponovanje sredstava.
        - Interna menjačnica za konverziju između različitih tipova tokena.
        """;

    private const string En = """
        # Technical Documentation — Closed-Loop Payment Tokenization System

        ## Project Overview
        This project implements tokenization of digital wallets within a closed-loop payment
        system, using blockchain technology, digital wallets, and tokens. The goal is to increase
        transaction security and protect user data, while preserving the advantages closed-loop
        systems already offer — lower transaction costs, stronger customer loyalty, and tighter
        control over data.

        The system is built as a microservice architecture in C# / .NET, with four independent
        microservices (WalletMS, TokenMS, TransactionMS, BlockchainMS) sitting behind a shared
        API Gateway.

        ## Motivation
        Closed-loop payment systems (e.g. internal company wallets, loyalty/bonus programs)
        traditionally offer lower transaction costs and better data control compared to open-loop
        systems (Visa, MasterCard), at the cost of reduced flexibility and limited usability
        outside their own ecosystem. The main remaining risk is security: centralized
        wallet-balance storage is vulnerable to data breaches, and the lack of independently
        verifiable proof of transactions makes auditing and trust-building harder.

        Tokenization, combined with writing transaction proofs to a public blockchain, addresses
        both problems: user funds are represented as tokens whose integrity can be
        cryptographically verified, and a summary of all transactions in a given period (a Merkle
        root) is written to an immutable, publicly accessible ledger.

        ## Key Concepts

        ### Open vs. Closed Payment Systems
        - **Open systems** (four-party model) involve the payer's bank, the payee's bank, and a
          centralized third party (e.g. Visa/MasterCard); more flexible, but more expensive per
          transaction.
        - **Closed systems** (three-party model) operate within a single company — transactions
          are authorized and processed on the same platform, enabling lower and more predictable
          costs.

        ### Digital Wallets and Tokens
        Wallets are categorized as open, semi-closed, or closed, depending on whether they allow
        payout to any account, transactions only within a partner network, or exclusively within
        a single company. Tokens represent digital assets — in this system, every token carries
        its own hash-signed data structure (balance, type, version, timestamp, transaction ID)
        that can be independently validated.

        ### Blockchain, Hashing, and the Merkle Tree
        A blockchain provides an immutable, decentralized ledger — every entry is protected by
        cryptographically hashing the previous state. The system uses SHA-256 to hash
        transactions and a **Merkle tree** to efficiently aggregate a large number of transactions
        into a single root, so that any change to any transaction in the set changes the root
        value and is immediately detectable.

        ### Microservice Architecture and the C4 Model
        The architecture is modeled using the C4 approach (Context → Containers → Components →
        Code), splitting the system into independent microservices that communicate over
        JSON/HTTP.

        ## Functional Requirements

        | ID | Description |
        |---|---|
        | R1 | User authentication and authorization |
        | R2 | Creating and managing digital wallets |
        | R3 | Creating and managing tokens |
        | R4 | Token versioning and support for multiple concurrent versions |
        | R5 | Token validation |
        | R6 | Creating transactions (deposit, payment, withdrawal) |
        | R7 | Transaction validation |
        | R8 | Writing proof of executed transactions to the blockchain |

        ## Non-functional Requirements

        | ID | Description |
        |---|---|
        | NR1 | The system must process at least 500 transactions per minute without response time degrading above 300ms (p95) |
        | NR2 | Wallet balance and token data must be encrypted at rest and in transit (TLS 1.2+) |
        | NR3 | The user → wallet-address mapping must be physically separated from token balance data |
        | NR4 | Every microservice must be independently deployable and horizontally scalable |
        | NR5 | The system must remain available (with degraded functionality) even when BlockchainMS is temporarily unavailable — blockchain writes are queued instead of blocking the transaction |
        | NR6 | Every token and transaction validation attempt is logged for audit purposes, without storing sensitive data in plain text |
        | NR7 | Recovery time (RTO) after a single microservice failure must not exceed 2 minutes |

        ## System Architecture

        ### Context
        Two user roles interact with the system: **employees** (execute payments) and
        **administrators** (additionally manage deposits and withdrawals). The system relies on
        an external authentication service (SSO) and communicates with a public blockchain to
        write transaction proofs.

        ### Containers

        | Container | Description | Technologies |
        |---|---|---|
        | API Gateway | System entry point — authentication, authorization, routing, rate limiting | ASP.NET, Yarp |
        | WalletMS | Creates and manages users' digital wallets and tokens | ASP.NET, EF Core, Redis, Azure Key Vault |
        | TokenMS | Creates, validates, and versions tokens | ASP.NET |
        | TransactionMS | Creates transactions, generates the Merkle tree | ASP.NET, EF Core |
        | BlockchainMS | Writes transaction proofs to the public blockchain | ASP.NET, Nethereum |
        | WalletDb / TransactionBankDb | Wallet and transaction persistence | SQL Server / SQLite |

        Everything behind the API Gateway is isolated within a private network.

        ### Components per Microservice

        **WalletMS** — the `Wallet Controller` exposes endpoints to the API Gateway,
        TransactionMS, and TokenMS, and coordinates wallet locking during a transaction via the
        `Redis Service`. The `Wallet Service` handles CRUD operations on wallets (through Entity
        Framework Core), while the `Azure Key Vault Service` securely stores the user →
        wallet-address mapping, kept separate from token balance data. The `Token Service`
        fetches and updates the tokens tied to a given wallet.

        **TokenMS** — a stateless service, easy to scale horizontally. Its `Token Service`
        implements the **Factory Method** pattern (`IToken` interface, `TokenFactory` class) for
        creating and updating tokens across versions, allowing new token versions to be
        introduced without modifying existing code.

        **TransactionMS** — the `Transaction Controller` accepts payment, deposit, and withdrawal
        requests. Initiated transactions are placed into an in-memory **transaction pool**; a
        background `Transaction Hosted Service` periodically (every 10 seconds in the current
        configuration) drains the pool, generates the Merkle root and validation paths, and
        forwards the root to BlockchainMS.

        **BlockchainMS** — the `Blockchain Service` writes the Merkle root to a smart contract on
        the Ethereum network (via the Nethereum library) and returns the blockchain transaction
        hash as proof of the write, which is then persisted alongside the corresponding bank
        transaction.

        ## Transaction Execution Flow

        A typical payment between two users within the system follows these steps:

        1. **Payment request** — The employee submits a payment request through the API Gateway,
           which verifies authentication (SSO token) and forwards the request to TransactionMS.
        2. **Wallet locking** — TransactionMS calls WalletMS to lock both the payer's and payee's
           wallets via the Redis Service, preventing the same wallet from being used in two
           concurrent transactions.
        3. **Token validation** — WalletMS fetches the current tokens for both wallets and calls
           TokenMS to validate their integrity (`IsTokenValid()`); if validation fails, the
           transaction is rejected immediately.
        4. **Transaction creation** — TransactionMS creates a transaction record (status
           `Pending`) and places it into the in-memory transaction pool, along with a snapshot of
           the tokens' state before the transaction (`TokenFromOld` / `TokenToOld`).
        5. **Token state update** — TokenMS generates new tokens with the updated balance (a new
           hash-signed structure) and returns them to WalletMS, which persists them and releases
           the wallet locks.
        6. **Periodic aggregation** — Every 10 seconds, the `Transaction Hosted Service` in
           TransactionMS drains all transactions added since the last cycle, generates the Merkle
           root (`GenerateMerkleRoot`), and computes a validation path for each transaction
           (`GetMerklePath`).
        7. **Blockchain write** — TransactionMS forwards the Merkle root to BlockchainMS, which
           calls the smart contract's `set` function on the Ethereum network and returns the
           blockchain transaction hash.
        8. **Transaction closure** — TransactionMS updates the transaction status to `Success`,
           stores `EtheriumProof` (the blockchain transaction hash) and the validation path
           alongside the corresponding token, making the transaction independently verifiable.

        If any step from 1 through 5 fails, the transaction is marked `Failed`, any locked wallets
        are released immediately, and no token is modified — the system remains in a consistent
        pre-transaction state.

        ## Key Implementation Mechanisms

        - **Hash-signed token structure** — every token field (balance, type, creation time,
          transaction ID, …) has an associated hash value; `IsTokenValid()` recomputes all hashes
          and compares them against the stored values, detecting any unauthorized modification.
        - **Wallet locking** — Redis is used to temporarily lock the wallets involved in a
          transaction, preventing double-spending from concurrent requests.
        - **Merkle tree** — `GenerateMerkleRoot` and `GetMerklePath` (SHA-256) aggregate pooled
          transactions into a single root and generate a validation path for each individual
          transaction, stored alongside the token as proof of inclusion in the written root.
        - **Smart contract** — a minimal Solidity contract (`set` / `get` over a single string
          field) receives the Merkle root and writes it to an Ethereum test network, producing
          publicly verifiable proof of the executed transactions.

        ## Security Considerations

        In addition to the hash-signed token structure and wallet locking described above, the
        system applies further measures:

        - **Separation of identity from balance** — the user → wallet-address mapping is stored
          exclusively in Azure Key Vault, while WalletDb and TransactionBankDb only contain
          addresses and tokens, with no direct link to user identity. Compromising either database
          alone does not reveal whose wallets are affected.
        - **Independent verification outside the system** — since the Merkle root is written to
          the public Ethereum network, any interested party (including external auditors) can
          independently verify that a given transaction is indeed included in the written root,
          without access to the system's internal databases.
        - **Rate limiting at the API Gateway** — mitigates abuse via bulk requests (e.g.
          brute-force token validation attempts).
        - **Principle of least privilege** — the administrator role is separate from the employee
          role and only has access to deposit/withdrawal functions; no role has direct database
          access.
        - **Known residual risks** — the system currently does not protect against a compromised
          Azure Key Vault account (the single point linking identity and wallet) and does not
          implement rotation of the keys used for hashing; both are identified as directions for
          further security hardening.

        ## Data Model

        Persistence is split across two databases: `WalletDb` (wallets and tokens) and
        `TransactionBankDb` (transactions). Both tokens and transactions store hash signatures of
        their key fields, enabling later integrity validation without re-reading the original
        source of truth. The user → wallet-address mapping is kept separately in Azure Key Vault
        — neither database contains that link.

        ### Table: Wallet

        | Column | Type | Description |
        |---|---|---|
        | Address | GUID (PK) | Unique wallet address |
        | Tokens | navigation (1:N) | Tokens currently tied to this wallet |

        ### Table: Token

        | Column | Type | Description |
        |---|---|---|
        | TokenId | GUID (PK) | Unique token identifier |
        | WalletAddress | GUID (FK → Wallet) | The wallet this token belongs to |
        | TokenData | JSON (string) | Serialized structure — balance, type, version, creation time, transaction ID, proof of inclusion in the Merkle tree |
        | TokenDataSignature | int | Hash signature of the whole TokenData block, used to detect unauthorized changes |

        ### Table: Transaction

        | Column | Type | Description |
        |---|---|---|
        | TransactionId | GUID (PK) | Unique transaction identifier |
        | TransactionType | enum | `Payment` / `Deposit` / `Withdraw` |
        | TokenFromOld / TokenToOld | JSON (string) | Snapshot of the involved tokens' state before the transaction (audit trail) |
        | TokenType | enum | The type of digital currency the transaction moves |
        | Amount | decimal | Transaction amount |
        | FeeFrom / FeeTo | decimal | Transaction fees per side |
        | TransactionStatus | enum | `Pending` / `Success` / `Failed` |
        | TransactionIniciator | string | Identifier of the user who initiated the transaction |
        | TransactionSignature | string (hash) | SHA-256 hash of the transaction — a leaf in the Merkle tree |
        | EtheriumProof | string | Hash of the blockchain transaction that wrote the root including this transaction |

        ### Enumerations

        | Enum | Values | Description |
        |---|---|---|
        | TransactionType | `Payment`, `Deposit`, `Withdraw` | The kind of operation the transaction represents |
        | TransactionStatus | `Pending`, `Success`, `Failed` | The transaction's current processing state |
        | TokenType | `Standard`, `Bonus`, `Promotional` | Regular funds, loyalty bonus points, or promotional credit |

        ## Key Code Excerpts

        ### Token versioning interface

        ```csharp
        public interface IToken
        {
            bool IsTokenValid();
            TokenDto ToTokenDto();
        }
        ```

        Every token version (`TokenV1`, and any future `TokenV2`) implements this interface.
        `TokenFactory` picks the right implementation based on the `Version` field in `TokenData`
        — this is the core of the Factory Method pattern described in the previous section.

        ### Token hash signature (abridged)

        ```csharp
        private void CalculateHashValues()
        {
            TokenData.BalanceHash = HashFunction(TokenData.Balance.ToByteArray());
            TokenData.VersionHash = HashFunction(TokenData.Version.ToByteArray());
            TokenData.CreatedHash = HashFunction(TokenData.Created.ToByteArray());
            TokenIdHash           = HashFunction(TokenId.ToByteArray());
            WalletAddressHash     = HashFunction(WalletAddress.ToByteArray());
            TokenDataSignature    =
                HashFunction(CreateIntArrayForSignatureHashCalculation().ToByteArray());
        }
        ```

        `IsTokenValid()` re-runs the same calculation and compares the result against the stored
        hash values — if any field changed outside this path, validation fails immediately.

        ### Merkle root generation (abridged)

        ```csharp
        public static string GenerateMerkleRoot(List<string> transactions)
        {
            var level = transactions.Select(HashFunc).ToList();
            while (level.Count > 1)
            {
                var next = new List<string>();
                for (int i = 0; i < level.Count; i += 2)
                    next.Add(i + 1 < level.Count
                        ? HashFunc(level[i] + level[i + 1])
                        : level[i]);
                level = next;
            }
            return level[0];
        }
        ```

        The function aggregates the hash values of all transactions in the current pool into a
        single root — that exact value is what gets written to the smart contract on-chain.

        ### Smart contract (Solidity)

        ```solidity
        // SPDX-License-Identifier: MIT
        pragma solidity ^0.8.0;

        contract SimpleStorage {
            string public storedData;

            function set(string memory x) public {
                storedData = x;
            }

            function get() public view returns (string memory) {
                return storedData;
            }
        }
        ```

        A minimal contract with a single string field — `set` writes the Merkle root, `get` reads
        it back for independent verification from any Ethereum client or explorer.

        ### Token factory (Factory Method)

        ```csharp
        public static class TokenFactory
        {
            public static IToken Create(TokenDataDto data) => data.Version switch
            {
                1 => new TokenV1(data),
                _ => throw new NotSupportedException($"Unsupported token version: {data.Version}")
            };
        }
        ```

        This static factory is the only place in the system aware of concrete `IToken`
        implementations; adding `TokenV2` is just a new `case` in the `switch` expression, with no
        changes to the code that consumes tokens.

        ### Merkle path generation (abridged)

        ```csharp
        public static List<string> GetMerklePath(List<string> hashes, int index)
        {
            var path = new List<string>();
            var level = hashes;
            while (level.Count > 1)
            {
                var isRightNode = index % 2 == 1;
                var pairIndex = isRightNode ? index - 1 : index + 1;
                if (pairIndex < level.Count)
                    path.Add(level[pairIndex]);

                var next = new List<string>();
                for (int i = 0; i < level.Count; i += 2)
                    next.Add(i + 1 < level.Count ? HashFunc(level[i] + level[i + 1]) : level[i]);

                level = next;
                index /= 2;
            }
            return path;
        }
        ```

        The path is stored alongside the token and lets anyone independently reconstruct the
        Merkle root starting from a single transaction's hash and this path, without access to all
        other transactions in the pool.

        ### Wallet locking (Redis)

        ```csharp
        public async Task<IAsyncDisposable> LockWalletAsync(Guid walletAddress, TimeSpan timeout)
        {
            var key = $"wallet-lock:{walletAddress}";
            var token = Guid.NewGuid().ToString();
            var acquired = await _redis.StringSetAsync(key, token, timeout, When.NotExists);

            if (!acquired)
                throw new WalletLockedException(walletAddress);

            return new RedisLockHandle(_redis, key, token);
        }
        ```

        Locking is implemented as a Redis `SET ... NX` with an expiry, avoiding a permanently
        locked wallet if the service holding the lock crashes unexpectedly.

        ### API Gateway routing (YARP configuration)

        ```json
        {
          "ReverseProxy": {
            "Routes": {
              "wallet-route": {
                "ClusterId": "wallet-cluster",
                "Match": { "Path": "/api/wallets/{**catch-all}" }
              },
              "transaction-route": {
                "ClusterId": "transaction-cluster",
                "Match": { "Path": "/api/transactions/{**catch-all}" }
              }
            },
            "Clusters": {
              "wallet-cluster": {
                "Destinations": { "d1": { "Address": "http://walletms:8080" } }
              },
              "transaction-cluster": {
                "Destinations": { "d1": { "Address": "http://transactionms:8080" } }
              }
            }
          }
        }
        ```

        The API Gateway uses YARP (Yet Another Reverse Proxy) to route requests to the appropriate
        microservice based on the path prefix; each cluster can list multiple destinations for
        horizontal scaling.

        ## API Overview

        All publicly exposed APIs pass through the API Gateway; internal calls between
        microservices are not exposed externally.

        ### WalletMS

        | Method | Path | Description |
        |---|---|---|
        | GET | `/api/wallets/{address}` | Returns basic wallet data and the list of associated tokens |
        | POST | `/api/wallets` | Creates a new wallet for the currently authenticated user |
        | POST | `/api/wallets/{address}/lock` | Internal call — locks a wallet for the duration of a transaction |
        | DELETE | `/api/wallets/{address}/lock` | Internal call — releases a wallet lock |

        ### TokenMS

        | Method | Path | Description |
        |---|---|---|
        | POST | `/api/tokens/validate` | Validates the integrity of the supplied token (`IsTokenValid`) |
        | POST | `/api/tokens` | Creates a new token of the given version via `TokenFactory` |
        | PUT | `/api/tokens/{tokenId}` | Updates an existing token's state after a transaction |

        ### TransactionMS

        | Method | Path | Description |
        |---|---|---|
        | POST | `/api/transactions/payment` | Initiates a payment between two wallets |
        | POST | `/api/transactions/deposit` | Initiates a deposit (administrators only) |
        | POST | `/api/transactions/withdraw` | Initiates a withdrawal (administrators only) |
        | GET | `/api/transactions/{id}` | Returns the status and details of a single transaction, including `EtheriumProof` once available |

        ### BlockchainMS

        | Method | Path | Description |
        |---|---|---|
        | POST | `/api/blockchain/proofs` | Internal call — writes the supplied Merkle root to the blockchain and returns the transaction hash |
        | GET | `/api/blockchain/proofs/{hash}` | Returns the confirmation status (confirmation count) for a given blockchain transaction hash |

        ## Evaluation
        The system was tested by comparing wallet balances after an identical sequence of
        transactions (deposit, payment, withdrawal) against an existing, non-tokenized
        closed-loop payment system. User balances matched exactly across both systems, confirming
        the correctness of the implemented logic. In addition, the transaction proof was
        successfully written to and located on the blockchain, and its value matches the
        corresponding transaction recorded in the database.

        In terms of performance, the existing non-tokenized system currently runs faster for a
        small number of transactions, but the implemented solution is expected to scale better as
        transaction volume grows, thanks to its stateless microservices (TokenMS, BlockchainMS)
        and Merkle-tree-based transaction aggregation.

        ## Testing Strategy

        - **Unit tests** cover token hash signing and validation (`CalculateHashValues`,
          `IsTokenValid`), Merkle root and validation path generation (`GenerateMerkleRoot`,
          `GetMerklePath`), and `TokenFactory`'s version-based implementation selection.
        - **Integration tests** exercise the full transaction flow across TransactionMS and
          WalletMS, using a test double for BlockchainMS (no real Ethereum testnet write), keeping
          the tests fast and deterministic.
        - **End-to-end scenarios** run periodically against an Ethereum test network, to confirm
          the smart contract correctly receives and returns stored values under realistic network
          latency.
        - **Comparison against a reference system** — as described in the Evaluation section,
          wallet balances are automatically compared against the results of the existing
          non-tokenized system for the same input data, as a regression check on correctness.

        ## Limitations
        - The system depends on an external Active Directory for authentication — if AD is
          unavailable, the entire system becomes unavailable.
        - Depositing funds currently requires physical contact with an administrator, limiting
          scalability.
        - `GenerateMerkleRoot` does not cap the number of transactions per tree — at high
          transaction frequency this can affect verification performance.
        - Writing to Ethereum is not free — gas cost fluctuates and must be factored into
          transaction fee design.
        - WalletMS and TransactionMS are stateful services, so scaling them requires more
          advanced coordination compared to the stateless TokenMS and BlockchainMS.

        ## Advantages
        - Increased transaction and data security through the combination of tokenization,
          wallet locking, and on-chain proof.
        - Lower, proportional transaction fees compared to open-loop payment systems.
        - Centralized access control via Active Directory integration (roles, password policies,
          multi-factor authentication).
        - Full independence from external payment providers and their regulatory constraints.

        ## Glossary

        | Term | Explanation |
        |---|---|
        | Closed-loop payment system | A payment system in which all transactions are processed within a single organization, without a third-party intermediary |
        | Tokenization | Replacing sensitive data (a balance) with a generated identifier (a token) whose integrity can be independently verified |
        | Merkle tree | A binary tree of hash values that enables efficient, secure verification of a large data set via a single root |
        | Merkle validation path | The sequence of hash values needed to reconstruct the Merkle root from a single transaction's hash, without requiring all other transactions |
        | Double spending | A situation where the same funds are spent more than once due to concurrent transactions — prevented via wallet locking |
        | Smart contract | A program that runs on a blockchain whose execution and result can be independently verified |
        | Gas (Ethereum) | The fee paid for executing operations on the Ethereum network, including writing data to a smart contract |
        | Transaction pool | A temporary in-memory store of transactions waiting to be aggregated into a Merkle root and written to the blockchain |

        ## Roadmap
        - Writing transaction proofs to multiple public blockchains simultaneously, for
          additional resilience.
        - Locking only the token type involved in a transaction, instead of the entire wallet.
        - Open banking integration for automated deposits.
        - An internal exchange for converting between different token types.
        """;
}
