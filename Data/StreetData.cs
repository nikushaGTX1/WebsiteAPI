using System.Collections.Generic;
using Website_API.Models;

namespace Website_API.Data
{
    public class StreetData
    {
        public static List<StreetModels> StreetsList = new()
        {   
            /////////////////////////// TBILISI ///////////////////////////
            // Vake-Saburtalo
            new() {
                Id = 1,
                City = "Tbilisi",
                Region = "Vake-Saburtalo",
                District = "Nutsubidze plateau",
                StreetNames = new List<string> {
                    "Bandzeladze st. ",
                    "E. Amashukeli st.",
                    "Levan Asatiani st.",
                    "P. Datuashvili st. st.",
                    "nucubidze I plato",
                    "nucubidze II plato",
                    "nucubidze III plato",
                    "nucubidze IV plato",
                    "nucubidze v plato",
                }
            },
            new() {
                Id = 2,
                City = "Tbilisi",
                Region = "Vake-Saburtalo",
                District = "Saburtalo",
                StreetNames = new List<string> { 
                    // 1
                    "26 May Square",
                    // A
                    "A.Bakradze st.",
                    "A.kalandaze st.",
                    "Abuladze st.",
                    "Aleksandre Basilaia's alley",
                    "Aleksandre Ioseliani II Lane",
                    "Anjaparidze II Lane",
                    "Antonovskaia st.",
                    "Aphaqidze st.",
                    "Archil Sulakauri St.",
                    // B
                    "B. Zghenti St.",
                    "B.Kvernadze st",
                    "Baratashvili dead end",
                    "Bedia St.",
                    "Bolkvadze st.", 
                    "Burdzgla st.",
                    // C
                    "Chakhava st.",
                    "Chiladze st.",
                    "Cxinvali st.",
                    // D
                    "Debi Ishxnelebi st.",
                    "Dolidze st.",
                    "Dzotsi st.",
                    // E
                    "E. Magalashvili st.",
                    // F
                    "Fanjikidze st.",
                    // G
                    "Gakhokidze st.",
                    "Gegechkori st.",
                    "Giorgi Danelia st.",
                    "Giorgi Saakadze II Ln",
                    "Gotua st.",
                    // H
                    "Heroes Square",
                    "Holy Martyr Vasilisko st.",
                    // I
                    "I.Gamrekeli st.",
                    "Ikalto st.",
                    "Iovel Jebashvili St.",
                    // K
                    "K. Kutateladze st.",
                    "Kafianidze st.",
                    "Kaman alley",
                    "Kamani st",
                    "Kapaneli St.",
                    "Kartozia st",
                    "Kazbegi avenue",
                    "Khomli st.",
                    // L
                    "L. Aleksidze st.",
                    // M
                    "Marijani st.",
                    "Maro Tarkhnishvili st.",
                    "Marshal Archil Gelovani Ave.",
                    "Mukhran Machavariani st.",
                    "Mzia eristavi st",
                    // O
                    "O.Chkheidze st.",
                    "O.Lortkipanidze st.",
                    "Ochamchiri st.",
                    // P
                    "P. Ioseliani st.",
                    "Politkovskaya St (jikia st.)",
                    // R
                    "R. Chkhikvadze st.",
                    // S
                    "S. Arshba st.",
                    "Saakadze Descent",
                    // T
                    "Tamarashvili st. (Saburtalo)",
                    "Tavkhelidze st.",
                    "Tsagareli St.",
                    // V
                    "Varden Tsulukidze st.",
                    // Z
                    "Z. Anjafaridze I lane",
                    "Z. Anjafaridze st.",
                    "Zaldastanishvili st",
                    // a
                    "a. gelovani st.",
                    "akhalsheni st.",
                    "akuri st.",
                    "al.ioseliani st.",
                    "ambrolauri st.",
                    "aslanidi st.",
                    // b
                    "bakhtrioni st.",
                    "bakurcikhe st.",
                    "balanchivadze st.",
                    "berbuki st.",
                    "beritashvili st.",
                    "bochorishvili st.",
                    "brolosani st.",
                    "budapeshti I turn",
                    "budapeshti II turn",
                    "budapeshti st.",
                    "bulachauri st.",
                    // c
                    "chabukiani st.",
                    "chailuri st.",
                    "chiatura st.",
                    "chikovani st.",
                    "cholokashvili avenue",
                    // d
                    "d. gamrekeli st.",
                    "daraselia st.",
                    "delisi I st.",
                    "delisi II st.",
                    "delisi st.",
                    "g. gabashvili st.",
                    "gagarin st.",
                    "gazapkhuli st.",
                    "gurieli st.",
                    // h
                    "h. alievi riverside",
                    // i
                    "ikalto mount",
                    "iona vakeli st.",
                    "iosebidze st.",
                    // j
                    "j. bagrationi st.",
                    "j. nadiradze",
                    // k
                    "kandelaki st.",
                    "kantaria st.",
                    "kareli st.",
                    "kavtaradze st.",
                    "khvichia st.",
                    "kodori st.",
                    "kostava st.",
                    "kutateladze st.",
                    // l
                    "likhauri dead end",
                    "likhauri st.",
                    "likhauri turn",
                    // m
                    "m. aleksidze st.",
                    "m. asatiani st.",
                    "margiani st.",
                    "megreladze st.",
                    "megreladze turn",
                    "mirotadze st.",
                    "mitskevichi st.",
                    // n
                    "nasidze st.",
                    "natakhtari st.",
                    "nuchubidze st.",
                    // p
                    "panaskerteli st.",
                    "pankisi st.",
                    "pekini st.",
                    "pkhakadze st.",
                    // s
                    "s. chikovani st.",
                    "s. cincadze st",
                    "sagarejo st.",
                    "sairme mount",
                    "sairme st.",
                    "sh. Mikeladze st.",
                    "shartava st.",
                    "shavgulidze st.",
                    "shavishvili st.",
                    "shevardenidze st.",
                    "skhirtladze st.",
                    // t
                    "t. buachidze",
                    "t. nadareishvili st.",
                    "tandzia st",
                    "tashkenti st.",
                    "tavadze st.",
                    "tekhura st.tkvarcheli st.",
                    "tsinaubani st.",
                    "tuskia st.",
                    // u
                    "universiteti st.",
                    // v
                    "v. chikovani st.",
                    "vaja pshavela I chikhi",
                    "vaja pshavela III turn,",
                    "vaja-pshavela avenue",
                    // z
                    "z. bendeliani st.",
                    "zakariadze st.",
                    "zurab zhvania square",
                }
            },
            new() { Id = 3, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Digomi Village",
                StreetNames = new List<string> {
                    // C
                    "Chelidze st.",
                    // D
                    "David Agmashenebeli st.",
                    "David Sarajishvili st.",
                    "Didgori st." ,
                    "Digomi village",
                    // F
                    "Firtzkheliani st.",
                    // G
                    "Gekhtmani st." ,
                    "Gela Gogishvili st",
                    "Giorgi Brtskinvale st.",
                    // K
                    "Kazbegi st.",
                    // N
                    "N. Baratashvili st.",
                    "Nenavi st.",
                    // S
                    "Shota Rustaveli st.",
                    "Sveticxoveli st.",
                    // T
                    "Teodore Tironi st.",
                    "Tetri Giorgi st.",
                    "Tsameti asureli mama st.",
                    // U
                    "Ugrelidze st.",
                    // V
                    "Vaja-Pshavela st.",        
                }
            },
            new() { Id = 4, City = "Tbilisi", Region = "Vake-Saburtalo", District = "District of Vazha-Pshavela",
                StreetNames = new List<string>
                {
                    // B
                    "Block I - Vazha-Pshavela" ,
                    "Block II - Vazha-Pshavela" ,
                    "Block III - Vazha-Pshavela" ,
                    "Block IV - Vazha-Pshavela" ,
                    "Block V - Vazha-Pshavela" ,
                    "Block VI - Vazha-Pshavela" ,
                    "Block VII - Vazha-Pshavela",
                    // S
                    "Sandro Euli st.",
                    // G
                    "G. Barnabishvili",
                    // M
                    "Mamasakhlisov st.",
                    "Mindeli st.",

                }

            },
            new() { Id = 5, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Lisi Lake",
            StreetNames = new List<string>
                {
                // A
                "A.Tabidze st.","Agaraki st.",
                "Avaliani st.",
                "Avto Varazi I lane",
                "Avto Varazi st.",
                // B
                "Bitsadze St.",
                // C
                "Chilaia st.",
                // D
                "Danelia st.",
                // I
                "Iasamnebi st.",
                "Intensification st.",
                // K
                "Kapaneli turn.",
                "Kvatchadze st.",
                // L
                "Lisi lake",
                "Lisi st.",
                // N
                "N. Dgebuadze St.",
                "Nekerchkhli st.",
                // S
                "Sharvadze St.",
                // T
                "T. Gudava st.",
                "Tirifebi st.",
                "Tsakhvebi st.",
                "Tsatsxvebi Lane IX",
                // s
                "st. Phermcerta"
            }
            },
            new() { Id = 6, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Turtle Lake",
             StreetNames = new List<string>
             {
                 // T
                 "Turtle lake",
             }

            },

            new() { Id = 7, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Bagebi",
                StreetNames = new List<string>
                {
                    // A
                    "A. Amaglobeli St.",
                    "Almond Orchards St.",
                    // D
                    "Dima Batiashvili st.",
                    "Dolidze st.",
                    // I
                    "I. Machabli st.",
                    "Imedadze st.",
                    // J
                    "Jansug Kordzaia St.",
                    // K
                    "Kaklebi st.",
                    // M
                    "Mcxeta st.",
                    // S
                    "Sakandelidze st.",
                    // T
                    "Tskneti Hwy I Line",
                    // U
                    "Uchaneishvili III Lane",
                    // V
                    "V. Tabliashvili St.",
                    // Z
                    "Zura Sakandelidze st.",
                    // t
                    "tskneti hwy",
                    // u
                    "uchaneishvili I turn",
                    "uchaneishvili II turn",
                    "uchaneishvili st."
                }
            
            },
            new() { Id = 8, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Didi Digomi",
                StreetNames = new List<string>
                {
                    // 1
                    "13 Asureli Mamis st.",
                    "4000 Meskhi st.",
                    // A
                    "Abashidze-Orbeliani st.",
                    "Akhaltsikheli st.",
                    "Andronikashvili st.",
                    "Archil Mephe st.",
                    "Asi atasi mocame st.",
                    "Asmati st.",
                    // B
                    "Baron de baia st." ,
                    "Bendukidze University Campus" ,
                    "Berta Fon Zutneri st." ,
                    "Berulava st.",
                    // C
                    "Chandari st.",
                    // D
                    "Danibegashvili st",
                    "Davari st." ,
                    "David Batonishvili st." ,
                    "Demetre Tavdadebuli st." ,
                    "Dighomistskali St.",
                    // E
                    "Ekvtime Kheladze st.",
                    "Eredvi st.",
                    // F
                    "Farsadani st.",
                    "Fatmani st.",
                    "Frederik monperi st.",
                    // G
                    "Georgian-American friendship avenue",
                    "Giorgi Nakulbakevi st.",
                    "Grigol Chkhikvadze st.",
                    "Gvirilebi st.",
                    // H
                    "Hainrikh Klaproti st.",
                    "Henrik Prinevski st.",
                    "Hugo Huperti st.",
                    // I
                    "I. shakarishvili st.",
                    "Ian Homeri st.",
                    "Ilia Sheklashvili St.",
                    "Isidore Dolidze st.",
                    // J
                    "Jakob Rainegsi st.",
                    "John-Malkhaz Shalikashvili st.",
                    "Joseph Tournefort St.",
                    // K
                    "Kasteli st.",
                    "Katalikos Abraham Ii st.,",
                    "Khataeti st.",
                    "Kristian Stiveni st.",
                    // L
                    "Lamberti st.",
                    "Levan Rcheulishvili st.",
                    // M
                    "Makashvili st.",
                    "Marko polo st." ,
                    "Melik-Surkhavi st." ,
                    "Mikha Khelashvili St." ,
                    "Mirian Mephe st." ,
                    "Mukhatgverdi St." ,
                    "Muradin-Fridoni st.",
                    // N
                    "Nestan-Darejani st.",
                    "Niko Buri st.",
                    // O
                    "Oskar Shmerlingi",
                    // P
                    "Paata Janiashvili St.",
                    // Q
                    "Qoshigora",
                    // R
                    "Ramazi st.",
                    "Rene Shmerlingi st.",
                    "Rostevani st.",
                    // S
                    "S. Mirianashvili st." ,
                    "S. Tskhakaia st." ,
                    "Shermadini st." ,
                    "Straboni st." ,
                    "Svanishvili st.",
                    // T
                    "Tamar mephe st.",
                    "Tavisufloebis st.",
                    "Teimurazi st.",
                    "Tinatin Virsaladze St.",
                    "Tinatini st.",
                    // U
                    "Udabno masteri lane I",
                    // V
                    "V.Chiladze st.",
                    "V.Tsintsadze st.",
                    "Vakhushti Batonishvili st.",
                    "Vepkhistkaosnis st.",
                    // Z
                    "Zukhbaya st.",
                    "Zurgovana",
                    // a
                    "agmashenebeli alley",
                    "avtandili st.",
                    // b
                    "bagrat III st.",
                    // g
                    "giorgi brtskinvale st.",
                    // i
                    "ioane petritsi st.",
                    // p
                    "parnavaz mepe avenue",
                    "petre imeri st.",
                    // t
                    "tarieli st.",
                }
            
            },
            new() { Id = 9, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Digomi 1-9",
                StreetNames = new List<string>
                {
                    // A
                    "Aisi II Lane",
                    "Aisi St.",

                    // G
                    "G.Chokheli st.",
                    "Givi Kvichidze st.",

                    // I
                    "Isis Lane I",

                    // K
                    "Kato Mikeladze St.",
                    "Kukuri Gogoiashvili St.",

                    // L
                    "Lasha Lashxia st.",

                    // N
                    "N. Beradze st.",

                    // V
                    "V. Nozadze st.",

                    // d
                    "digomi 1",
                    "digomi 2",
                    "digomi 3",
                    "digomi 4",
                    "digomi 5",
                    "digomi 6",
                    "digomi 7",
                    "digomi 8",
                    "digomi 9",

                    // g
                    "g. peradze st."
                }
            },
            new() { Id = 10, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Vake", 
                StreetNames = new List<string>
                {
                    // A
                    "Abuladze st.",
                    "Ateni st.",
                    "Avalishvili st.",
                    // C
                    "Chabua Amirejibi hwy",
                    // G
                    "Ghoghoberidze st.",
                    // I
                    "Ilia Chavchavadze I lane",
                    // J
                    "Janashvili St.",
                    // K
                    "Khabeishvili st.",
                    // L
                    "L. Bielefeld St.",
                    "L.Mikeladze st.",
                    // N
                    "Nino Ramishvili's Dead End II",
                    // R
                    "R.Chkhikvadze st.",
                    // S
                    "Shoshitaishvili st.",
                    "Svanidze st.",
                    // T
                    "Taktakishvili St",
                    "Tengiz Akhmeteli St.",
                    // a
                    "a.razmadze st." ,
                    "abasheli st." ,
                    "arakishvili st.",
                    "areshidze st.",
                    // b
                    "bazaleti st.",
                    "berdzenishvili st.",
                    "burkiashvili st.",
                    // c
                    "cereteli turn",
                    "cholokashvili st.",
                    "ckhvedadze st.",
                    "ckneti st.",
                    // d
                    "dariali st.",
                    // e
                    "e. takhaishvili st.",
                    // g
                    "gabashvili st.",
                    "gr. abashidze (ateni) st.",
                    "gr. mukhadze st.",
                    // i
                    "i. abashidze st.",
                    "i. chavchavadze avenue",
                    // k
                    "k. kldiashvili st.",
                    "kavsadze st." ,
                    "nkekelidze st.",
                    "kipshidze st." ,
                    "kobuleti st." ,
                    "kutateladze st.",
                    // l
                    "lezhava st.",
                    // m
                    "m. abashidze st.",
                    "marabda st." ,
                    "marukhis gmirebi st." ,
                    "mckheta st." ,
                    "mishveladze st." ,
                    "mosashvili st.",
                    // n
                    "n. zhvania st." ,
                    "napareuli st." ,
                    "nino ramishvili st.",
                    // p
                    "paliashvili st.",
                    // r
                    "r. eristavi st.",
                    "radiani st.",
                    // s
                    "sajaia st." ,
                    "shatberashvili st." ,
                    "shovi st." ,
                    "shrosha st.",
                    // t
                    "t. tabidze st.",
                    "tamarashvili st. (vake)",
                    // v
                    "varaziskhevi st.",
                    // z
                    "zemo vake st.",
                }
            
            },
            new() { Id = 11, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Vashlijvari",
                StreetNames = new List<string>
                {
                    // A
                    "Arzakan Emukhvari st.",
                    // B
                    "Brotseuli st.",
                    // I
                    "I. Farjiani st.",
                    "Iasamani st.",
                    // M
                    "Mukhran Machavariani st.",
                    // V
                    "V. Topuridze st.",
                    // g
                    "gelovani avenue",
                    "godziashvili I turn",
                    "godziashvili II turn",
                    "godziashvili III turn",
                    "godziashvili st.",
                    // k
                    "kvantaliani st.",
                    // s
                    "sarajishvili st."
                }
            
            },
            new() { Id = 12, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Vedzisi",
                StreetNames = new List<string>
                {
                    // A
                    "A.Andronikashvili st.",
                    "Afkhaidze st.",
                    "Ardaziani st.",
                    "Artmelidze st.",
                    // E
                    "E.Cherkezishvili st.",
                    // H
                    "H.Abashidze st.",
                    "H.Abashidze st.",
                    // I
                    "Ilia Odishelidze st.",
                    "Imedashvili st.",
                    "Isakadze st.",
                    // L
                    "Levandovski st.",
                    "Lomonosovi st.",
                    "Lvovi st.",
                    // M
                    "M.Gelovani st.",
                    "Mamia Alasania st.","" +
                    "Mgaloblishvili st.",
                    // O
                    "Odesa st.",
                    "Oniashvili st.",
                    "Oseti st.",
                    // S
                    "Sakhokia st.",
                    // Z
                    "Zemo Vedzisi blind alley I",
                    "Zemo Vedzisi blind alley II",
                    "Zemo Vedzisi st.",
                    "Zovreti st.",
                }


            },
            new() { Id = 13, City = "Tbilisi", Region = "Vake-Saburtalo", District = "Tkhinvala",
               StreetNames = new List <string> 
               { 
                   // I
                   "I. Kechakmadze st",
                   "Ioseb Kechakmadze St.",
                   // T
                   "Tkhinvala",
               }

            },

            // Isani-Samgori
            new() { Id = 14, City = "Tbilisi", Region = "Isani-Samgori", District = "Airport village",
                StreetNames = new List<string>
                {
                    // I
                    "Igoeti st.",
                    // Q
                    "Qeburias dasaxleba",
                    // a
                    "airpot settlement",
                }
            },
            new() { Id = 15, City = "Tbilisi", Region = "Isani-Samgori", District = "Dampalo village",
                StreetNames = new List<string>
                {
                    // D
                    "Dampalo Village"
                }
            },
            new() { Id = 16, City = "Tbilisi", Region = "Isani-Samgori", District = "Vazisubani",
                StreetNames = new List<string>
                {
                    // A
                    "Achabeti St.",
                    // C
                    "Cherry St.",
                    // I
                    "I Microdistrict - Vazisubani",
                    "II Microdistrict - Vazisubani",
                    "III Microdistrict - Vazisubani",
                    "IV Microdistrict - Vazisubani",
                    // J
                    "Jumber Lezhava Lane",
                    // K
                    "Kvachantiradze st.",
                    // M
                    "Marshal Jozef Pilsudski str.",
                    // Q
                    "Qobuladze st.",
                    // T
                    "Teophane Davitaia st.",
                    // U
                    "Udzo st.",
                    // V
                    "Vazi st.",
                    "Vazisubnis dasaxleba",
                    // d
                    "davitashvili st.",
                    // m
                    "mshvelidze st.",
                    "muskhelishvili st.",
                    // p
                    "pataridze st.",
                    // r
                    "ratili st.",
                    // s
                    "shandor petef st.",
                    // t
                    "tvishi st."
                } 
            },
            new() { Id = 17, City = "Tbilisi", Region = "Isani-Samgori", District = "Varketili",
               StreetNames = new List <string> 
               {
                   // 1
                   "17 Shindiseli gmiri st.",
                   // A
                   "A. Chxenkeli st.",
                   // D
                   "D. Aleksidze st.",
                   "Dzeglevi st.",
                   // F
                   "Fighters for the Unity of Georgia st.",
                   // I
                   "I Microdistrict - Varketili",
                   "II Microdistrict - Varketili" ,
                   "III Microdistrict - Varketili" ,
                   "IV Microdistrict - Varketili" ,
                   "IV Microdistrict, II rigi",
                   // K
                   "Kadir Shervashidze St.",
                   // L
                   "Lado Kotetishvili St.",
                   "Lagidze st.",
                   "Liza Nakashidze-Bolkvadze st.",
                   // M
                   "Mshvidobis st.",
                   // N 
                   "Nairashvili St.",
                   // O
                   "Ojaleshi st.",
                   // S
                   "Sadgeri St.",
                   "Sukhishvili st.",
                   // T
                   "Tbilisi Sea new city",
                   "Tetritskaro st.",
                   // V
                   "Varketilis meurneoba",
                   // Y
                   "Yotam Zedgenidze St.",
                   // Z
                   "Z. Mkhargdzeli st.",
                   "Zurab Chachua st.",
                   // a
                   "aerodromi village",
                   // c
                   "cnorischkali",
                   // e
                   "eldari st.",
                   // g
                   "g. beriashvili st.",
                   "gakhokidze st.",
                   "giorgadze st.",
                   // j
                   "javakheti st.",
                   // k
                   "khomeli st.",
                   "kupradze st.",
                   // l
                   "landia st.",
                   // m
                   "maisuradze st.",
                   "microdistrict II - Varketili zemo plato",
                   // t
                   "trialeti st.",
                   "tvalchrelidze st.",
               }
            },
            new() { Id = 18, City = "Tbilisi", Region = "Isani-Samgori", District = "Isani",
                StreetNames = new List<string>
                {
                    // A
                    "Abramishvili st.",
                    "Ahmed Javadi st." ,
                    "Aleksandre Kalandadze st" ,
                    "Amilakhvari St." ,
                    "Arsen Ikaltoeli St.",
                    // B
                    "Baqarashvili st.",
                    "Beri Gabriel Salosi Ave.",
                    "Bogdan Khmelnytskyi IV Lane",
                    "Bukitsikhi st.",
                    // D 
                    "Dolidze St.",
                    // E
                    "E. Porakishvili-Sarajishvili st.",
                    // H 
                    "Holburiki st.",
                    // I 
                    "II kheivani",
                    "Iunkerta st.",
                    // J
                    "Jan Pribyl St.",
                    "Jiqia st.",
                    // K
                    "Kakabeti st.",
                    "Kakheti Highway (Isani)",
                    // L
                    "L. Abashidze st.",
                    "Lekh Kachinski st.",
                    "Lobjanidze st.",
                    // M
                    "Marjorie Wardrop's 2nd Lane",
                    // N
                    "Noe khomeriki st.",
                    // O
                    "Olga Bakhutashvili st.",
                    // P
                    "P.Djanelidze st.",
                    // S
                    "Sh. Nadirashvili st.",
                    "Shorapani st.",
                    "Sidamon-Eristavi St.",
                    "Simon Kldiashvili st.",
                    // V
                    "Vanler Daisel st.",
                    "Vasil Koptsov St.",
                    // a
                    "ackuri st.",
                    "amirejibi st." ,
                    "andguladze st." ,
                    "apanasiev st." ,
                    "arveladze st.",
                    // b
                    "bagdadi st.",
                    "bochormi st.",
                    // c
                    "calenjikha st.",
                    "cernaki st.",
                    "chrelashvili st.",
                    // d
                    "d. kacharava st.",
                    "dodashvili st.",
                    "doesi st.",
                    "dolabauri I st.",
                    "dolabauri III st.",
                    "dolabauri gorge" ,
                    "durmishidze st." ,
                    "dzmebi orbelianebi st.",
                    // e
                    "ekimi st.",
                    // g
                    "gare kakheti st.",
                    "gezati st.",
                    "gulua st.",
                    // i
                    "i. janelidze st.",
                    "ialbuzi st.",
                    "isnis dead end",
                    // j
                    "jorj bushi st.",
                    // k
                    "k. bakradze st." ,
                    "ketevan tsamebuli avenue (isani)" ,
                    "khachaturiani st." ,
                    "kharabadze st." ,
                    "kharkharashvili st." ,
                    "nkhvareli st.",
                    // m
                    "matiashvili st.",
                    "melaani st.",
                    "meskheti st.",
                    "mtisdziri st.",
                    "mtisdziri turn",
                    // n
                    "navtlugi st.",
                    // o
                    "oniani st.",
                    // p
                    "patardzeuli st.",
                    // s
                    "s. durmishidze st.",
                    "sabargo village",
                    "shervashidze st",
                    "shiraqi st.",
                    // t
                    "tolenji st.",
                    // u
                    "ujarma st.",
                    "ushakov st.",
                    // v
                    "vik. naneishvili st.",
                    // z
                    "z. anjaparidze st.",
                    "zindisi st.",
                    "zurabishvili st.",

                }
            
            },
            new() { Id = 19, City = "Tbilisi", Region = "Isani-Samgori", District = "Lilo",
            StreetNames = new List<string>
            {
                // A
                "A.Mgeladze st.",
                // C
                "Chelidze st.",
                "Chikvaidze I lane",
                "Chikvaidze st.",
                // D
                "Davit Kobakhidze Street",
                "Devdariani St.",
                // E
                "Evgeni Apkhazava st.",
                // I
                "Iumashevi st.",
                // K
                "Kakheti Highway (Lilo)",
                // L
                "Lortkipanidze st.",
                // S
                "Sturua st.",
                // T
                "Tamar mephe st.",
                "Tba st.",
                "The third alley of Erekle II",
                // c
                "cereteli st.",
                "chirnakhuli st.",
                // g
                "glonti st.",
                "gokieli st.",
                // m
                "meprinveleoba st.",
                // r
                "rustaveli st.",
                // s
                "shanshaishvili st."
            }

            },
            new() { Id = 20, City = "Tbilisi", Region = "Isani-Samgori", District = "Mesame masivi",
                StreetNames = new List<string>
                {
                    // A
                    "Abuladze st.",
                    "Abashvili st.",
                    // I
                    "Irina Shtenberg st.",
                    // J
                    "Javakheti alley",
                    // K
                    "Kakheti Highway (III masivi)",
                    "Kaloubani st.",
                    // M
                    "Mesame masivi",
                    // P
                    "Panaskerteli-Tsitsishvili st.",
                    // S
                    "Shuamta st."
                }

            
            },
            new() { Id = 21, City = "Tbilisi", Region = "Isani-Samgori", District = "Ortachala",
                StreetNames = new List<string>
                {
                    // A
                    "Artur Laisti st.",
                    // B
                    "Badagi st.",
                    "Beqa and Bushqen opizrebi st.",
                    "Berznis st.",
                    // G
                    "G. Guramishvili st.",
                    "G. Volski St",
                    "G.Nikoladze st.",
                    "Gorgasali II turn",
                    // I
                    "I kheivani st.",
                    "II kheivani st.",
                    // K
                    "Krtsanis I Ln",
                    // M
                    "Melkadze st.",
                    "Mount Tabor I lane",
                    "Mount Tabor St.",
                    // N
                    "N.Tabidze st.",
                    "Narimanovi st",
                    "New Khevni st.",
                    "Nikoloz (Carlo) Chkheidze st.",
                    // S
                    "Sabatone alley",
                    "Suliko Tortladze St.",
                    // T
                    "The second lane of Krtsanis",
                    "Tsolikauri st.",
                    // U
                    "Usakhelauri st.",
                    // V
                    "Vezirovi st.",
                    // a
                    "algeti st.",
                    // b
                    "baazov st.",
                    "berdznis dead end",
                    // c
                    "curtaveli st.",
                    // d
                    "dekabristebi st.",
                    "didi kheivani st.",
                    // e
                    "efrem mcire st.",
                    // g
                    "gorgasali I turn",
                    "gorgasali st.",
                    "grishashvili st.",
                    "gulia square",
                    "gulia st.",
                    // i
                    "i. Sabanisdze st.",
                    // k
                    "kalandadze st.",
                    "kharpukhi st.",
                    "khoneli st." ,
                    "koda turn" ,
                    "krtsanisi st.",
                    "kumsiashvili st.",
                    // l 
                    "lordkipanidze st.",
                    // m
                    "martvili st.",
                    "merchule st.",
                    "mirza shah st.",
                    // n
                    "nadikvari st.",
                    "nizami st.",
                    // o
                    "opreti st.",
                    "ortachala st.",
                    // s
                    "shavnabada st.",
                    "soganlugi turn",
                    // t
                    "tabakhmela st.",
                    "teleti st.",
                    "tsalka st.",
                    "tsinanauri st.",
                    // z
                    "zviad gamsakhurdia st."

               }


            },
            new() { Id = 22, City = "Tbilisi", Region = "Isani-Samgori", District = "Orkhevi",
                StreetNames = new List<string>
                {
                    // A
                    "Amilakhvari St.",
                    // C
                    "Chantladze I lane","Chantladze st.",
                    // D
                    "Damenia st.",
                    // M
                    "Mshenebeli st.",
                    // T
                    "Tetri Khevi st.",
                    // Z
                    "Zaza Damenia 1st Lane","Zmebi slovinskebi st.",
                    // a
                    "abzianidze st.","akhvlediani st.",
                    // d
                    "d. kakabadze st.",
                    // e
                    "esenin st.",
                    // m
                    "mukhadze st.",
                    // s
                    "saakadze st.","sh. Amiranashvili st.",
                    // t
                    "tsutsunava st."
                }
            },
            new() { Id = 23, City = "Tbilisi", Region = "Isani-Samgori", District = "Samgori",
                StreetNames = new List<string>
                {
                    // A
                    "A. Dadiani st.",
                    "Alaverdeli St.",
                    "Antimoz Iveriel st.",
                    "Azmaifarashvili st.",
                    // B
                    "Bagdavadze st.",
                    "Begiashvili st.",
                    // E
                    "Emir Burjanadze st.",
                    // G
                    "Grigorashvili st.",
                    // I
                    "Ilia Babutsidze st.",
                    // K
                    "Kakheti Highway (Samgori)",
                    "Kakliani st.",
                    "Khvedeliani st.",
                    "Khvedelidze st.",
                    "Kindzmarauli Lane",
                    "Kindzmarauli Lane II",
                    "Kvirikashvili st.",
                    // M
                    "M.Mdivani st.",
                    "Mamisashvili st.",
                    // O
                    "Onashvili st.",
                    "Osip Mandelshtam st.",
                    // P
                    "Perini st.",
                    // S
                    "S. Takaishvili st.",
                    "Samgori settlement",
                    "Sergo Gujejiani st.",
                    "Settlement of St. Barbara st.",
                    // T 
                    "Tekla Batonishvili st.",
                    // Y
                    "Yalno st.",
                    // Z
                    "Z. larajuli St.",
                    // c
                    "chochua st.",
                    // k
                    "kairo st.",
                    "kindzmarauli st.",
                    "kopcov st.",
                    // m
                    "moskovi ave",
                    // n 
                    "nakaduli I turn",
                    "nakaduli st.",
                    // o
                    "ockheli st.",
                    // t
                    "tsulukidze st."

                }
            
            
            },
            new() { Id = 24, City = "Tbilisi", Region = "Isani-Samgori", District = "Ponichala",
                StreetNames = new List<string>
                {
                    // A
                    "Afkhazeti st.",
                    // B
                    "Bagrati st.",
                    "Brodvei st.",
                    // E
                    "Erekle Lane",
                    "Erekle St.",
                    "Evstati Mtskheteli st.",
                    // G
                    "Gogoberidze st.",
                    "Gulbani st.",
                    "Gziskari st.",
                    // I
                    "Ilori st.",
                    // M
                    "Marneuli hwy",
                    // N
                    "Nokalakevi St.",
                    // P
                    "Pataraia st.",
                    "Ponichala 3",
                    // R
                    "Romanoz Razmadze st.",
                    "Rustavi hwy",
                    // S
                    "S. Surguladze st.",
                    "Sokhumi st.",
                    "Spiridon Virsaladze St.",
                    // V
                    "Vagzali st.",
                    // a
                    "a. jordania st.",
                    // m
                    "marelisi st.",
                    "marneuli st."
                }
            
            },
            new() { Id = 25, City = "Tbilisi", Region = "Isani-Samgori", District = "Airport",
                StreetNames = new List<string>
                {
                    // A
                    "Airport road"
                }
            
            },
            new() { Id = 26, City = "Tbilisi", Region = "Isani-Samgori", District = "Afrika",
                StreetNames = new List<string>
                {
                    // A
                    "Afrika",
                    "Aladasturi st.",
                    // D
                    "D.Tsurtsumia st.",
                    // E
                    "Enukidze st.",
                    // G
                    "Gugushvili st.",
                    "Gvazava st.",
                    // I
                    "Idumala st.",
                    // K
                    "K. Sanadze st.",
                    // L
                    "Levan Razikashvili st.",
                    "Luka Razikashvili St.",
                    // T
                    "Ts. Amirejibi st.",
                    "Tsarafi St.",
                    "Tsiskarishvili st.",
                    "Turashauli st.",
                    // b
                    "b. chichinadze st.",
                    // g
                    "gumathesi st."
                }
            
            
            },
            new() { Id = 27, City = "Tbilisi", Region = "Isani-Samgori", District = "Navtlugi"
            , StreetNames = new List < string >
            {
                // G
                "Getia st.",
                // M
                "Metreveli st.",
                // N
                "Nadareishvili st.",
                "Navtlughi IV st",
                "Navtlughi V St.",
                // a
                "abesuridze tbeli st.",
                // b
                "b. Khmelnytskyi II",
                "b. khmelnicki st",
                "nbaladini st.",
                "bolnisi st.",
                // c
                "charkhi st.",
                "chkalovi st.",
                "cikhisdziri",
                // d
                "didgori st.",
                "dirsichala st.",
                // e
                "engurhetsi st.",
                // g
                "gardabani road",
                "gr. lordkipanidze st.",
                "gudarekhi st.",
                // h
                "hospital st.",
                // j 
                "joresi st.",
                // k
                "khevi st.",
                "nkiziki st.",
                // l
                "lertsami st.",
                // m
                "malkhazi st.",
                "manavi st.",
                "matiashvili st.",
                "meveli st.",
                // n
                "navtlugi I st.",
                "ninidze st.",
                // r
                "rtveladze st.",
                // s
                "sakhalkho st.",
                "samgori st.",
                // t
                "tatishvili st.",
                "tsuladze st.",
            }
            
            },

            // Gldani-Nadzaladevi
            new() { Id = 28, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Avchala",
                StreetNames = new List<string>
                {
                    // A
                    "A. Vasadze st.",
                    "Aphkhazeti st.",
                    // B
                    "Baghashvili st.",
                    "Bichvinta st.",
                    // C
                    "Chavchavadze st.",
                    // D
                    "D. Tavdadebuli St.",
                    "Dumbadze St.",
                    // E
                    "Egrisi st.",
                    "Elver Kupatadze I Exit",
                    // F
                    "Farnavaz mefe st",
                    // G
                    "G.Tabidze st.",
                    "Gela Chedia st.",
                    "Gigo Khechuashvili st.",
                    "Guria st.",
                    // H
                    "Hereti st.",
                    // J
                    "Janjgava st.",
                    "Javakheti I lane",
                    "Javakheti III Ave.",
                    "Jorjoliani st.",
                    // K
                    "Kacharava st.",
                    "Kushitashvili st.",
                    // L
                    "Leonidze st.",
                    // M
                    "M.Mrevlishvili st.",
                    // O
                    "Orbeliani st.",
                    // R
                    "Racha st.",
                    "Rioni Street",
                    // S
                    "S.Takaishvili st.",
                    "Samegrelo St.",
                    // U
                    "Utseri st.",
                    // V
                    "Vakhtan Gorgasali St.",
                    "Vardevani st.",
                    "Vitali Daraselia st.",
                    // a
                    "a. managadze st.",
                    // b
                    "baghnari st.",
                    "baraleti st.",
                    "barisakho st.",
                    // c
                    "chrebalo st.",
                    // d 
                    "didgori st.",
                    // e
                    "e. andronikashvili st.",
                    // g
                    "gagra st.",
                    // i
                    "i. Grishashvili st.",
                    "ilori st.",
                    "ipolitov ivanov st.",
                    // l 
                    "libani st",
                    // p
                    "pockhishvili st.",
                    // r
                    "rioni st.",
                    // s
                    "shusha st."

                }
            
            },
            new() { Id = 29, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Gldani",
                StreetNames = new List<string>
                {
                    // A
                    "A Microdistrict - Gldani",
                    // B
                    "Berthubani st.",
                    "Bochorishvili st.",
                    "Borchaloeli st.",
                    // D
                    "Davit Jabidze st.",
                    "Drandi st.",
                    // E
                    "E. Kharadze st.",
                    // G
                    "Giorgi Babilodze St.",
                    // I
                    "I Microdistrict - Gldani",
                    "II Microdistrict - Gldani",
                    "III Microdistrict - Gldani",
                    "III a Microdistrict - Gldani",
                    "IV Microdistrict - Gldani",
                    "Ingusheti st.",
                    // K
                    "Khergiani st.",
                    "Khevispuri st.",
                    "Kula Gldaneli St.",
                    // L
                    "Leselidze st.",
                    // M
                    "Marseli st.",
                    "Memed Abashidze st.",
                    "Mosulishvili st.",
                    // P
                    "Parsman II Kveli st.",
                    // S
                    "Shengelia st.",
                    "Sokhumi st.",
                    // T
                    "T. Botchorishvili st.",
                    "Teimuraz Bochorishvili's I dead end",
                    "Teimuraz Bochorishvili's II Dead End",
                    // V
                    "VI Microdistrict - Gldani",
                    "V Microdistrict - Gldani",
                    "VII Microdistrict - Gldani",
                    "VIII Microdistrict - Gldani",
                    // b
                    "baratashvili st.",
                    // d
                    "d.sarajishvili st.",
                    // g
                    "gldanis gorge st.",
                    "gldanula das.",
                    "gmiri kursantebi st.",
                    "gulisashvili st.",
                    // i
                    "i. vekua st.",
                    // k
                    "kandelaki st.",
                    "kerchi st.",
                    "khizanishvili st.",
                    "koberidze st.",
                    // m
                    "maisuradze st.",
                    "marjanishvili st.",
                    "mebagishvili st.",
                    "megobroba st.",
                    "midelauri st.",
                    "mikatadze st.",
                    "moreti st.",
                    // r
                    "razmadze st.",
                    // s
                    "sabashvili st.",
                    "sheshelidze st.",
                    // t
                    "tianeti hwy",
                    "tikhonov st.",
                    "tskalsadeni st.",
                    // v
                    "vartagava st.",
                    "vasadze st."
                                    }
            
            },
            new() { Id = 30, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Zahesi",
                StreetNames = new List<string>
                {
                    // A
                    "Avchala st.",
                    // C
                    "Chichinadze st.",
                    // E
                    "Energetic st.",
                    // F
                    "Freedom st.",
                    // G
                    "G.Makashvili st.",
                    // K
                    "Kaskadi st.",
                    "Kecxoveli st.",
                    "Khanzteli st.",
                    // M
                    "M. Tsinamdzghvrishvili",
                    "Mshvidobis st.",
                    // P
                    "Platini st.",
                    // S
                    "Shalva Apkhaidze III lane",
                    "Shotadze st.",
                    "Sxvitori st.",
                    // e
                    "e. andronikashvili st. (Zahesi)",
                    // k
                    "kazbegi st."
                }
            
            
            },
            new() { Id = 31, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Gldanula",
                StreetNames = new List<string>
                {
                    // 1
                    "26 Maisi st.",
                    // i
                    "i.kacharava st.",
                    // j 
                    "janjgava st.",
                    // l
                    "lazo st.",

                }
            
            
            },
            new() { Id = 32, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Tbilisi sea",
                StreetNames = new List<string>
                {
                    // E
                    "Eqimebi st.",

                    // T
                    "Tbilisi sea"
                }
            
            
            },
            new() { Id = 33, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Temqa",

                StreetNames = new List<string>
                {
                    // A
                    "Anapa st.",
                    "Avsahni st.",
                    "Avshni III Lane",
                    // G
                    "Giorgi Boterashvili Street",
                    "Guram Qutateladze st.",
                    // L
                    "Levan Rcheulishvili st.",
                    // N
                    "Nijaradze st.",
                    "Nushi st.",
                    // T
                    "T. Putkaradze St.",
                    "Tavgorashvili St.",
                    // V
                    "Veistsikhe st.",
                    // c
                    "chargali st.",
                    "codnis kari st.",
                    // k
                    "khevdzmari st.",
                    "khvanchkara st.",
                    // l
                    "lekishvili st.",
                    // m
                    "magalashvili st.",
                    // p
                    "p. amiranashvili st.",
                    // s
                    "sachkhere st.",
                    "sadmeli st.",
                    "shatili st.",
                    // t
                    "tkibuli st.",
                    // u
                    "ureki st.",
                    // M
                    "Micro. I, bl. IV - Temka",
                    "Micro. I, bl. IX - Temka",
                    "Micro. I, bl. X - Temka",
                    "Micro. I, bl. Xa - Temka",
                    "Micro. I, bl. Xb - Temka",
                    "Micro. III, bl. I - Temka",
                    "Micro. III, bl. II - Temka",
                    "Micro. III, bl. III - Temka",
                    "Micro. III, bl. IV - Temka",
                    "Micro. III, bl. V - Temka",
                    "Micro. XI, bl. I - Temka",
                    "Micro. XI, bl. II - Temka",
                    "Micro. XI, bl. III - Temka",
                    "Microdistrict IV - Temka",
                    "Monadire st."
                                    }
            
            
            },
            new() { Id = 34, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Koniaki village",
                StreetNames = new List<string>
                {
                    // K 
                    "Koniaki Village",
                    // M
                    "Monastery St.",
                    "Monastery dead end",
                    // T
                    "Tadzari st.",
                    // Z
                    "zarzma st.",
                }
                
            
            
            },
            new() { Id = 35, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Lotkini",
                StreetNames = new List<string>
                {
                    // E
                    "E. Zakaraia st.",
                    "Eliso Chipashvili st.",
                    "Elo Andronikashvili st.",
                    // G
                    "Gareji st.",
                    // L
                    "Laituri st.",
                    "Lotkini",
                    // S
                    "Samson Pirtskhalava St.",
                    // s
                    "sanavardo st.",
                    // t
                    "tseronisi st."
                }
            
            },
            new() { Id = 36, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Nadzaladevi",
                StreetNames = new List<string>
                {
                    // A
                    "Agiashvili st.",
                    // D
                    "Dzmoba II st.",
                    // G
                    "G. Asatiani St.", 
                    "Gulriphshi st.", 
                    "Guramishvili ave (Nadzaladevi)",
                    // I
                    "Internati st.",
                    "Ioseliani I turn",
                    "Ioseliani st.",
                    // K
                    "Kikvidze Lane", 
                    "Kikvidze st.", 
                    "Konskaia st.",
                    "Kurdiani st.",
                    // L
                    "L.Qartlelishvili st.",
                    // N
                    "Ninua st.", 
                    "Nodar Jangirashvili Street",
                    // R
                    "Rkinigza st.",
                    // T
                    "Ts. Dadiani st. (nadzaladevi)",
                    // U
                    "Utsnob gmirta st.",
                    // V
                    "Vasil Tamarashvili st.",
                    // a
                    "a. janelidze st.",
                    "adjara st.", 
                    "akhalkalaki st.",
                    "akhtala st.",
                    "argveti st.", 
                    "askhini st.", 
                    "askurava st.", 
                    "asureti st.", 
                    "atoci st.",
                    // b
                    "bagineti st.",
                    "bakhmaro st.",
                    "bendeliani m", 
                    "bezhanishvili st.",
                    "bogvi st.",
                    "bugeuli st.",
                    // c
                    "ch. bendeliani st.",
                    "chkhikvadze st.",
                    "chkondideli st.", 
                    "chokhatauri st.",
                    "cisartkela st.", 
                    "ckvitishvili st.",
                    // d
                    "d. chichinadze st.",
                    "d. jgenti st.",
                    "darkveti st.", 
                    "depo st.", 
                    "didi jikhaishi st.",
                    "dobrolubov st.",
                    // e
                    "eniseli st.",
                    // g
                    "g. eristavi st.", 
                    "g. saakadze st.",
                    "geguti st.", 
                    "gogoberidze st.",
                    "gomareti st.",
                    "gombori st.",
                    "gorda st.",
                    "grmagele st.", 
                    "gruzinski st.",
                    "gudushauri st.", 
                    "gurgenidze st.",
                    // i
                    "iluridze st.", 
                    "imereti st.",
                    "inanishvili st.",
                    // j
                    "java st.",
                    // k
                    "kachreti",
                    "kakhiani st.",
                    "kalujni st.", 
                    "kartli st.", 
                    "kartvelishvili st.",
                    "kavtiskhevi st.",
                    "keburia st.", 
                    "keleptrishvili st.",
                    "khertvisi st.", 
                    "khevsureti st.",
                    "khimshiashvili st.",
                    "khresili st.",
                    "khudadovi st.",
                    "khvingia st.", 
                    "kirimi st.",
                    "kishinev st.",
                    "kisiskhevi st.", 
                    "kldekari st.", 
                    "knolev st.", 
                    "kodalo st.", 
                    "kokhreidze st.",
                    "kokinaki st.", 
                    "kondoli st.", 
                    "ksovreli st.", 
                    "kursebi st.", 
                    "kvaloni st.",
                    // l
                    "lamisyana st.", 
                    "lechkhumi st.",
                    "likani st.", 
                    "lisashvili st.",
                    "lomtatidze st.", 
                    "lotkini gorge", 
                    "lotkini m",
                    "lubovski st.",
                    // m
                    "m. kavtaradze st.",
                    "m. meskhi st.", 
                    "machkhaani st.", 
                    "magaro st.",
                    "makhinjauri st.",
                    "manglisi st.", 
                    "manjgaladze st.",
                    "meqanizacia st.",
                    "mirzaani st.", 
                    "mitingi st.", 
                    "molokov st.",
                    "mrevlishvili st.", 
                    "mushata st.",
                    // n
                    "nakalakevi st.", 
                    "natadze st.", 
                    "niko mari st.", 
                    "noste st.",
                    // o
                    "orbeti st.",
                    // p
                    "pilia st.",
                    // r
                    "rionhesi st.",
                    // s
                    "s. chilaia st.", 
                    "sachilao st.",
                    "sakviri st.", 
                    "samgereti st.", 
                    "samokalako st.", 
                    "samshvilde st.", 
                    "sanerge st.",
                    "sarkineti st.",
                    "sartania st.", 
                    "sh. cincadze st.", 
                    "sh. mikatadze st.", 
                    "shatilov st.",
                    "sturua st.", 
                    "sumbatashvil-iujni st.",
                    "sumorov st.", 
                    "svaneti st.", 
                    "sviri st.",
                    // t
                    "tmogvi", 
                    "tordia st.", 
                    "traktori st.",
                    "trikotaji st.",
                    "tsagveri st.", 
                    "tsiklauri st.",
                    "tsitsamuri st.",
                    "tskaro st.",
                    "tsromi st.",
                    // u
                    "uiarago st.",
                    "uridia st.",
                    // v
                    "veshapuri st.",
                    // z
                    "zakaria st.",
                    "zedamze st.", 
                    "zedaubani",
                    "zedazeni st.", 
                    "zeinkali st.", 
                    "zekari st.",
                    "zestaponi st.",
                    "ziari"
                }
           
            
            
            },

            new() { Id = 37, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Sanzona",
                StreetNames = new List<string>
                {
                    // C
                    "Chargali st. (Sanzona)",
                    // D
                    "David Garejeli st.",
                    // G
                    "Gamkrelidze st.",
                    // J
                    "Jvarisubani St.",
                    // K
                    "Khevisubani st.",
                    // a
                    "a. baramidze st.",
                    "a. shengelaia st.",
                    "aketi st.",
                    "akhaldaba st.",
                    "araleti st.",
                    "arbo st.",
                    // b
                    "bjoleti st.",
                    // c
                    "choporti st.",
                    // d
                    "daisi st.",
                    "dviri st.",
                    // e
                    "edisi st.",
                    "ertso",
                    // g
                    "garikuli st.",
                    "glinka st.",
                    "gogasheni st.",
                    "gudamakari st.",
                    "guramishvili ave (Sanzona)",
                    "gvazauri st.",
                    // i
                    "ipani st.",
                    // j
                    "jinvali st.",
                    "jvelauri st.",
                    // k
                    "khornabuji st.",
                    "ksani st.",
                    "kvishkheti st.",
                    // l
                    "liakhvi st.",
                    // m
                    "moliti st.",
                    "mukhrani st.",
                    // n
                    "n.buachidze st.",
                    "nagomari st.",
                    // p
                    "peikrebi st.",
                    // r
                    "ratevani st.",
                    // s
                    "sikharulidze",
                    // t
                    "t.eristavi st. (Sanzona)",
                    "tianeti st.",
                    "tibaani st.",
                    "toroshelidze st.",
                    // v
                    "v.kakabadze st.",
                    "vardisubani st.",
                    "vaziani st.",
                    // z
                    "zigzagi st.",
                    "zvareti st."
                }
            
            },
            new() { Id = 38, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Gldani Village" ,
                StreetNames = new List<string>
                { 
                    // G
                    "Gldani village",
                    // K
                    "Kakha Torchinava St.",
                    // P
                    "Pirosmani II Lane",

                }
            
            },
            new() { Id = 39, City = "Tbilisi", Region = "Gldani-Nadzaladevi", District = "Ivertubani",
                StreetNames = new List<string>
                {
                    // A
                    "Akhalmocameta st.", 
                    "Alaverdi st.",
                    // B
                    "Barakoni st.", 
                    "Beshenova st.", 
                    "Betashvili st.",
                    // G
                    "Gabiskiria st.", 
                    "Gabunia st.", 
                    "Geno Adamia st.",
                    // J
                    "Jordania st.",
                    // K
                    "Kancheli st.", 
                    "Kherkheulidze st.", "" +
                    "Khornauli st.", 
                    "Khudadovi st. " +
                    "(Ivertubani)", 
                    "Kubaneishvili st.", 
                    "Kvaracxelia st.",
                    // L
                    "Liptovi st.",
                    // M
                    "Makhata Rise", 
                    "Marine Urtmelidze St.", 
                    "Meri Shervashidze st.", 
                    "Mizandari st.",
                    // N
                    "Nikoloz Agiashvili I lane",
                    // O
                    "Orjonikidze-Toroshelidze st.",
                    // R
                    "Raul Eshba st.", "Rekhi st.",
                    // S
                    "Samgereti st. (Ivertubani)",
                    // V
                    "Vano Khornauli st.",
                    // Z
                    "Zakaria Djordjaze st.",
                    // i
                    "ivertubani st."
                }
            
            },

            // Didube-Chughureti
            new() { Id = 40, City = "Tbilisi", Region = "Didube-Chughureti", District = "Didube",
                StreetNames = new List<string>
                {
                    // B
                    "B. Paichadze st.",
                    // D
                    "D.Kifiani st.",
                    // E
                    "Egnate and Vakhtang Fifia st.",
                    // K
                    "Kosmonavtebi coast",
                    // M
                    "M.Lebanidzei st.",
                    // S
                    "S.Metreveli st.",
                    // T
                    "T.Fifia st.",
                    // a
                    "a. kereseliZi st.", 
                    "a.qurdiani st.", 
                    "a.wereTlis avenue", 
                    "abastumani st.", 
                    "aglazde st.",
                    // b
                    "bagi st.", 
                    "bakradze st.", 
                    "batumi st.",
                    // c
                    "cabadze st.", 
                    "cemi st.",
                    // d
                    "didube st.",
                    // e
                    "evdoshvili st.",
                    // g
                    "gaprindauli st.",
                    "general kvinitadze st.",
                    "gogolauri st.",
                    "gori st.", 
                    "gudauta st.", 
                    "gvetadze st.",
                    // i
                    "iamanidze st.",
                    // k
                    "karaleti st.", 
                    "kedia st.",
                    "khosarauli st.",
                    "kutaisi st.",
                    // m
                    "maiakovski st.", 
                    "mirckhulava st.",
                    // p
                    "poti st.",
                    // s
                    "samtredia st.", 
                    "sokhumi st.", 
                    "stanislavski st.",
                    // t
                    "t. eristavi st. (Didube)",
                    "tevdore mgvdeli st.",
                    "transporti st.", 
                    "tskaltubo st.",
                    // v
                    "v.bagrationi st.", 
                    "vani st.",
                    "voronin st.",
                    // z
                    "zugdidi st."
                }
            
            },
            new() { Id = 41, City = "Tbilisi", Region = "Didube-Chughureti", District = "Digomi",
                StreetNames = new List<string>
                {
                    // A
                    "Agmashenebeli Alley (Digomi)", 
                    "Akhmeteli st.",
                    // B
                    "Bakradze st.",
                    "Block I - Digomi massive", 
                    "Block II - Digomi massive",
                    "Block III - Digomi massive",
                    "Block IV - Digomi massive", 
                    "Block V - Digomi massive",
                    "Block VI - Digomi massive", 
                    "Bob walsh st.",
                    // D
                    "Disevi st.",
                    // E
                    "E. Botsvadze st.",
                    "Esma Oniani st.",
                    // G
                    "Gr. Robakidze Ave",
                    // R
                    "Rondeli st.",
                    // S
                    "Shalva Gogidze St.",
                    // T
                    "Tereverko st.",
                    // b
                    "balanchini st.", 
                    "beliashvili st.", 
                    "bokhou st.",
                    // c
                    "chachava st.",
                    "chiaureli st.",
                    // l
                    "lubliana st."
                                    }
            
            },
            new() { Id = 42, City = "Tbilisi", Region = "Didube-Chughureti", District = "Kukia",
                StreetNames = new List<string>
                {
                    // C
                    "Chaladidi St.",
                    // D
                    "Dodo and Kote Khimshiashvili St.",
                    // K
                    "Khoni St.",
                    // L
                    "Lailashi St.", 
                    "Lanchkhuti St.",
                    // M
                    "Michurini St", 
                    "Mzia Jincharadze St.",
                    // S
                    "Senaki St.",
                    // T
                    "Teklati st.",
                    // a
                    "abasha st.",
                    // c
                    "caishi st.",
                    // g
                    "givishvili st.",
                    // k
                    "kapanadze st.",
                    // l
                    "larekhi st.",
                    "lebarde st.",
                    // m
                    "maruashvili st.",
                    // n
                    "norio st.",
                    // r
                    "rostov st.",
                    // s
                    "sakari st.",
                    "salkhino st.",
                    "samurzakano st.",
                    // u
                    "upliscikhe st."
                }
            
            },
            new() { Id = 43, City = "Tbilisi", Region = "Didube-Chughureti", District = "Svanetis ubani",
                StreetNames = new List<string>
                {
                    // A
                    "Akhalarsenali St.", 
                    "Aspindza St.",
                    // E
                    "E. Chavchavadze st.",
                    // L
                    "Lentekhi st.",
                    // N
                    "Nino and Kalistrate Saliebi st.",
                    // P
                    "P. Bagrationi St.",
                    // T
                    "Tamarasheni st.",
                    // a
                    "artvini st.",
                    // b
                    "bodavi I st.",
                    // c
                    "chechelashvili st.",
                    // l
                    "lami st.",
                    // m
                    "mamardashvili st.", 
                    "mnatobi st.",
                    // r
                    "r. davitashvili st."
                }
            
            
            },
            new() { Id = 44, City = "Tbilisi", Region = "Didube-Chughureti", District = "Chugureti",
                StreetNames = new List<string>
                {
                    // 1
                    "9 dzma st.",
                    // D
                    "Dondua st.",
                    // E
                    "E.Akhvlediani st.",
                    // G
                    "Gayane Khachaturiani St.", 
                    "Gogoli st.",
                    "Gogotur Agladze St.",
                    "Gumati st.",
                    // K
                    "Kakhidze st.", 
                    "Kankawa st.",
                    // L
                    "Lalioni lane",
                    "Larexi st.",
                    "Loladze st.",
                    // M
                    "M. Dadiani-Anchabadze st.",
                    // N
                    "N.Chkheidze st.",
                    // O
                    "Olive st.",
                    "Osiauri st.",
                    // Q
                    "Qvlividze st.",
                    // R
                    "Romi st.",
                    // T
                    "Tashiri St.",
                    "Tkeshelashvili st.",
                    "Ts. Dadiani st. (chugureti)",
                    // V
                    "V. Kikvidze st.",
                    // Z
                    "Z. Kurdiani St.",
                    // a
                    "adigeni st.",
                    "agara st.",
                    "agmashenebeli ave",
                    "ajameti st.", 
                    "akhalcikhe st.", 
                    "akhuti st.",
                    "alvani st.", 
                    "anaklia st.", 
                    "antelava st.", 
                    "apkhazeti st.", 
                    "aragvispireli st.",
                    "ardoni st.",
                    // b
                    "bakhvi turn", 
                    "bako st.",
                    "baratashvili rise",
                    "benashvili st.", 
                    "betania st.", 
                    "bodavi II st.", 
                    "bukhaidze st.",
                    // c
                    "chaisubani st.", 
                    "chanchibadze st.",
                    "chemishevski st.",
                    "chikobava st.", 
                    "chitaia st.", 
                    "chorokhi st.",
                    "chubinashvili st.", 
                    "chubinidze st.", 
                    "cimakuridze st.", 
                    "cinamdzgrishvili st.", 
                    "cuckiridze st.",
                    // d
                    "d. abashidze st.",
                    "d. berishvili st.",
                    "d. kldiashvili st.", 
                    "dgebuadze st.", 
                    "didkhevi rise",
                    "digombari st.", 
                    "dzegami st.",
                    // g
                    "g. cereteli st.", 
                    "g. dolidze st.", 
                    "g. zaziashvili st.", 
                    "galavani st.", 
                    "gamcemlidze st.", 
                    "gantiadi st.",
                    "gociridze st.",
                    "gogiberidze st.",
                    "gorki st.", 
                    "gremi turn", 
                    "grozno st.",
                    // i
                    "i. javakhishvili st.",
                    // k
                    "k. abashidze st.",
                    "kaishauri st.", 
                    "kalandarishvili st.", 
                    "kargareteli st.", 
                    "kharagauli dead end",
                    "khetagurov st.", 
                    "kiev st.", "kinkladze st.",
                    "konstitucia st.",
                    "kumisi st.",
                    "kvaliti st.",
                    // l
                    "lochini st.",
                    // m
                    "makhaldiani st.",
                    "mamradze gorge", 
                    "mamradze st.",
                    "marjanishvili square",
                    "marjanishvili st.",
                    "mazniashvili st.", 
                    "meunargia st.",
                    // n
                    "nadiradze st.",
                    "natishvili st.", 
                    "ninoshvili st.",
                    // o
                    "orbeliani square", 
                    "orbeliani st.",
                    // p
                    "p. saakadze st.",
                    "pabricius st.",
                    "paliastomi st.",
                    "paravani st.", 
                    "paster st.", 
                    "piatigorsk st.", 
                    "pirosmani st.", 
                    "platoni st.",
                    "pockhverishvili st.",
                    // s
                    "s. cereteli st.", 
                    "sankt-peterburg st.", 
                    "sh. mikatadze st.", 
                    "shakriani st.", 
                    "sighnagi st.", 
                    "skhulukhia st.", 
                    "somkheti st.", 
                    "st. nikolozi st.",
                    "suliashvili st.",
                    "sundukian st.",
                    "surami st.",
                    // t
                    "tamar mepe ave", 
                    "terenti graneli st.", 
                    "tetelashvili st.",
                    "tetnuldi st.",
                    "tkviavi st.", 
                    "toidze st.",
                    "tolstoi st.", 
                    "tovstonogov st.", 
                    "turgenev st.",
                    // u
                    "u. chkheidze st.",
                    "uricki st.",
                    "uznadze st.",
                    // v
                    "varcikhe st.",
                    "vedzatkhevi st.",
                    "verharni st.",
                    // z
                    "z. chavchavadze st.",
                    "zaarbruken square", 
                    "zaarbruken st."
                }
            
            
            },

            // Old Tbilisi
            new() { Id = 45, City = "Tbilisi", Region = "Old Tbilisi", District = "Abanotubani",
                StreetNames = new List<string>
                {
                    // A
                    "Abano I dead end",
                    "Abano II Cikhi",
                    "Abano st.",
                    "Akhundov st.",

                    // F
                    "Factory Lane",
                    "Firdousi st.",
                }
            
            
            },
            new() { Id = 46, City = "Tbilisi", Region = "Old Tbilisi", District = "Avlabari",
                StreetNames = new List<string>
                {
                    // A
                    "Abuladze st.", 
                    "Asatiani st.",
                    // C
                    "Chekhov st.",
                    "Chikhladze st.",
                    // E
                    "Erevani st.",
                    // G
                    "Gabeskiria st", 
                    "Gia Badridze Street",
                    // K
                    "K. Eristavi st.", 
                    "Khivi Turn",
                    // L
                    "Lomouri st.",
                    // S
                    "Samreklo st.",
                    "Solomon Brdzeni St.", 
                    "Spartaki st.",
                    // T
                    "Telavi st.", 
                    "Tirifoni st.", 
                    "Tsutsqiridze st.",
                    // V
                    "Vaja Iverieli st.", 
                    "Victor Jorbenadze st.",
                    // a
                    "akhmeti st.",
                    "aladashvili st.",
                    "alazni st.", 
                    "alikhaniani st.", 
                    "aragveli st.",
                    "ararati st.", 
                    "arboSiki st.", 
                    "armazi st.", 
                    "avlabri st.",
                    // b
                    "bachanas st.", 
                    "badiauri st.", 
                    "betania st.", 
                    "bodbe st.", 
                    "borodin st.",
                    // c
                    "ciskri st.",
                    "cuckhvaTi st.",
                    // d
                    "d.megreli st.", 
                    "dedoplistskaros st.", 
                    "dmanisi st.", 
                    "dusheti st.", 
                    "dzeveri st.",
                    // g
                    "gabriel ep. st.",
                    "gedevanishvili st.", 
                    "gelati st.",
                    "gonashvili st.",
                    "gujareTi st.",
                    "gumbri st.",
                    "gurjaani st.",
                    "gutani st.",
                    // i
                    "iori st.", 
                    "irbakhi st.", 
                    "isani st.", 
                    "izashvili st.",
                    // k
                    "kaspi st.", 
                    "ketevan tsamebuli avenue (avlabari)", 
                    "khakhanashvili st.",
                    "kherkheulidze st.",
                    "khidistavi st.", 
                    "khurkhuli st.", 
                    "koshkovani st.",
                    // l
                    "lagodekhi st.", 
                    "lusiniani st.",
                    // m
                    "makhatas st.", 
                    "martkopi st.",
                    "meskhishvili st.",
                    "metekhi rise", 
                    "metekhi st.", 
                    "mtavarangelozi st.",
                    // n
                    "nasakirali st.", 
                    "nioradze st.", 
                    "nosiri st.",
                    // o
                    "observatria st.",
                    // p
                    "paghavas st.",
                    "periscvaleba st.", 
                    "posta st.",
                    // r
                    "razikashvili st.",
                    "ruisi st.",
                    // s
                    "sabaduri st.", 
                    "sartichala st.",
                    "sevani st.", 
                    "shavsopeli st.", 
                    "shorapni II st.",
                    "spandariani st.",
                    // t
                    "tsinandali st.", 
                    "tsurtsumia st.",
                    // u
                    "ubilava st.", 
                    "urbnisi st.",
                    // v
                    "vakhtanq VI st.",
                    // w
                    "wine aRmarTi",
                    // z
                    "zakharovi st."
                }
            
            },
            new() { Id = 47, City = "Tbilisi", Region = "Old Tbilisi", District = "Elia",
                StreetNames = new List<string>
                {
                    // D
                    "Dorminika Eristavi st.",
                    // M 
                    "Mujirishvili st.",
                    // S
                    "Samxedro qalaqi 202",
                    "Sharadze st.",
                    // a
                    "abuli st.",
                    "akhalubani st.",
                    // b
                    "babiskhevi st.",
                    "begleti st.",
                    "bzipi st. ",
                    // j
                    "joneti st.",
                    // k
                    "kiketi st.",
                    // m
                    "m. mrevlishvili st.",
                    // n
                    "nakerala st.",
                    "niabi I turn",
                    "niabi II turn",
                    "niabi III turn",
                    "niabi IV turn",
                    "niabi V turn",
                    "niabi st.",
                    // o
                    "odzisi st.",
                    // p
                    "pshavi st.",
                    // r 
                    "ruispiri st.",
                    // s 
                    "saingilo st.",
                    "shilda st.",
                }
            
             
            },
            new() { Id = 48, City = "Tbilisi", Region = "Old Tbilisi", District = "Vera",
                StreetNames = new List<string> 
                {
                    // B
                    "Belinsky st.",
                    // J
                    "Jugel st.",
                    // K
                    "Kote Makharadze st.",
                    // M
                    "Machavariani st.",
                    // N
                    "Natia Bashaleishvili st.",
                    // R
                    "R. Chkheidze St.",
                    // S
                    "Sativ St.",
                    // T
                    "Tumanishvili st.",
                    // a
                    "ananuri st.",
                    "anjaparizde st.",
                    "aragvi st.",
                    // b
                    "barnovi st.",
                    // c
                    "chovelidze st.",
                    // d
                    "d. bakradze st.",
                    // e
                    "e. tatishvili st.", 
                    "ekaladze st.", 
                    "eristav-khoshtaria st.",
                    // g
                    "g. akhvlediani st.", 
                    "g. imedashvili st.", 
                    "gali st.", 
                    "gambashidze st.", 
                    "gogebashvili st.",
                    "goglidze st.",
                    "gudauri st.", 
                    "gunia st.",
                    // i
                    "i. Gurgulia st.", 
                    "i. kereselidze st.", 
                    "i. nikoladze st.",
                    // j
                    "janashia st.",
                    // k
                    "khorava st.", 
                    "kiacheli st.", 
                    "kobakhidze st.",
                    "kostava st.", 
                    "kuchishvili st.",
                    // l
                    "l. bocvadze st.", 
                    "larsi st.",
                    // m
                    "m. javakhishvili st.",
                    "makashvili st.", 
                    "melikishvili st.", "" +
                    "milorava st.",
                    "miminoshvili st.",
                    // n
                    "n. nikoladze st.",
                    // p
                    "petriashvili st.",
                    // r
                    "r. japaridze st.", 
                    "rcheulishvili st.", 
                    "rodeni st.",
                    // s
                    "s. chiaureli st.", 
                    "shanidze st.", 
                    "sharashidze st.", 
                    "shengelia st.",
                    "shio mgvimeli st.",
                    // t
                    "tarkhnishvili st.",
                    "tergi st.",
                    // u
                    "umikashvili st.",
                    // v
                    "vashlovani st.", 
                    "vera turn",
                    // z
                    "zandukeli st."
                }
            
                        
            },
            new() { Id = 49, City = "Tbilisi", Region = "Old Tbilisi", District = "Mtatsminda",
                StreetNames = new List<string>
                {
                    // 1
                    "8 march st.",
                    "9 april st.",
                    // F
                    "Farajanov st.",
                    // K
                    "K. Ukleba St",
                    "Khidi st.",
                    // M
                    "M. Japaridze st.",
                    "Meskhia st.",
                    "Mosidze st.",
                    // R
                    "R.Muskhelishvili st.",
                    // S
                    "Sanapiro st.",
                    "Suliko Jgenti st.",
                    // T
                    "T.Jordania st.",
                    "Tarieli st.",
                    "Tbilisi st.",
                    // V
                    "V.Orbeliani st.",
                    "Vedzinis st.",
                    // Z
                    "Z. Gamsakhurdia st.",
                    "Zedgenidze st.",
                    "Zichi st.",
                    // a
                    "a. kutateladze st.",
                    "nabakelia st",
                    "al. chavchavadze",
                    "aleksi-meskhishvili st.",
                    "amagleba st.",
                    "amagleba turn",
                    "arsena st.", 
                    // b
                    "baratashvili st.",
                    "beridze st.",
                    "besiki st.",
                    "bocvadze st",
                    "bolo rise st.",
                    "borbalo st.",
                    "brose st.",
                    // c
                    "cavkisi st." ,
                    "chaikovski st." ,
                    "chanturia st." ,
                    "chitadze st." ,
                    "chonkadze st.",
                    "ckhemi st.",
                    // e
                    "e. gabashvili st.",
                    // f
                    "freedom square",
                    // g
                    "griboedov st.",
                    "gudiashvili square",
                    "gudiashvili st.",
                    "guria st.",
                    // i
                    "ingorokhva st.",
                    // j
                    "jambuli st.",
                    "jorjadze st.",
                    // k
                    "k. meskhi st." ,
                    "kakabadzeebi st." ,
                    "kipiani st." ,
                    "kojori hwy" ,
                    "kojori st." ,
                    "kotetishvili st.",
                    "kurski st." ,
                    "kvali st.",
                    // l
                    "lesia ukrainka st.",
                    // m
                    "m. lagidze st." ,
                    "makashvili rise" ,
                    "mama daviti rise" ,
                    "mtacminda st.",
                    // n
                    "niaghvari st." ,
                    "nishnianidze st.",
                    // o
                    "odzelashvili st.",
                    "okrokana st.",
                    // p
                    "p. kakabadze st.",
                    "pkhovi st.",
                    "purceladze st.",
                    // r
                    "r. lagidze st.", 
                    "rica st.", 
                    "rustaveli avenue",
                    // s
                    "s. meskhi st.", 
                    "savaneli st.", 
                    "shevchenko st.", 
                    "shindisi st.",
                    // t
                    "tabukashvili st.",
                    // v
                    "v. abashidze st.", 
                    "v. sarajishvili st.",
                    "v. vekua st.",
                    "vachnadze st.", 
                    "vejini st.", 
                    "virsaladze st.",
                    // z
                    "z. bocvadze st.",
                    "z. chichinadze st.", 
                    "z. kikodze st.", 
                    "zaldastanishvili st.",
                    "zubalashvilebi st.", 
                    "zurab zvania st."

                }

            },
            new() { Id = 50, City = "Tbilisi", Region = "Old Tbilisi", District = "Sololaki",
                StreetNames = new List<string>
                {
                    // E
                    "Erekle II Lane",
                    // K
                    "Kote Abkhaz st.",
                    // S
                    "Shkhepi St.", 
                    "Sultnishani st.",
                    // V
                    "Vakhtang Beridze St",
                    // a
                    "a. katalikosi dead end", 
                    "abesadze st.", 
                    "abo tbileli st.",
                    "abovian st.",
                    "akhospireli st.", 
                    "akopian st.",
                    "amagleba st.", 
                    "askana st.",
                    "atoneli st.", 
                    "avlevi st.",
                    // b
                    "bambis rigi", 
                    "betlemi st.",
                    "bneli dead end", 
                    "botanikuri st.",
                    // c
                    "chakhrukhadze st.",
                    // d
                    "dabakhana st.",
                    "diuma st.", "dutu megreli st.",
                    // e
                    "erekle II st.",
                    // g
                    "g. tabidze st.",
                    "gergeti st.", 
                    "gogchi st.",
                    "gomi st.", 
                    "gr. khandzteli st.",
                    // i
                    "iashvili st.", 
                    "ierusalimi st.", 
                    "ietim gurji st.",
                    "irgvlivi st.", 
                    "iveria st.",
                    // j
                    "jibladze st.",
                    // k
                    "k. cincadze st.",
                    "kavlashvili st.", 
                    "khodasheni st.", 
                    "kikodze st.", 
                    "kuptin st.",
                    // l
                    "l. asatiani st.", 
                    "leonidze st.", 
                    "lermontov st.",
                    // m
                    "machabeli st.",
                    "mtkvari st.",
                    // n
                    "nakashidzeebi st.",
                    // o
                    "okromchedlebi st.",
                    "oni st.",
                    "orpiri st.",
                    // p
                    "petkhaini st.",
                    "pushkin st.",
                    // r
                    "rkinis rigi turn",
                    // s
                    "saadi st.", 
                    "saiatnova st.",
                    "salami st.", 
                    "samgebro st.", 
                    "sh. Dadiani st.", 
                    "shardeni st.", 
                    "shavteli st.", 
                    "sioni st.", 
                    "sobchak st.", 
                    "sololakis alley", 
                    "sulkhan-saba st.",
                    "sulkanishvili st.",
                    // t
                    "telegrapi st.", 
                    "tumaniani st.",
                    // v
                    "verckhli st."
                }
            
            },

            /////////////////////////// BATUMI ///////////////////////////
            new() { Id = 51, City = "Batumi", Region = "Districts of Batumi", District = "Airport District" },
            new() { Id = 52, City = "Batumi", Region = "Districts of Batumi", District = "Agmashenebeli District" },
            new() { Id = 53, City = "Batumi", Region = "Districts of Batumi", District = "Bagrationi District" },
            new() { Id = 54, City = "Batumi", Region = "Districts of Batumi", District = "Boni-Gorodok District" },
            new() { Id = 55, City = "Batumi", Region = "Districts of Batumi", District = "Tamar Settlement" },
            new() { Id = 56, City = "Batumi", Region = "Districts of Batumi", District = "Kakhaberi District" },
            new() { Id = 57, City = "Batumi", Region = "Districts of Batumi", District = "Rustaveli District" },
            new() { Id = 58, City = "Batumi", Region = "Districts of Batumi", District = "Old Batumi District" },
            new() { Id = 59, City = "Batumi", Region = "Districts of Batumi", District = "Khimshiashvili District" },
            new() { Id = 60, City = "Batumi", Region = "Districts of Batumi", District = "Javakhishvili District" },
            new() { Id = 61, City = "Batumi", Region = "Districts of Batumi", District = "Makhinjauri" },
            new() { Id = 62, City = "Batumi", Region = "Districts of Batumi", District = "Pivzaod district" },

            /////////////////////////// KUTAISI ///////////////////////////
            new() { Id = 63, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Avangardi Settlement" },
            new() { Id = 64, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Avtokarkhana Settlement" },
            new() { Id = 65, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Asatiani Settlement" },
            new() { Id = 66, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Aghmashenebeli Settlement" },
            new() { Id = 67, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Balakhvani" },
            new() { Id = 68, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Bzholebi" },
            new() { Id = 69, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Gabashvili Hill" },
            new() { Id = 70, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Gora Sakuslia" },
            new() { Id = 71, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Gumathesi" },
            new() { Id = 72, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Vakisubani" },
            new() { Id = 73, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Zastava" },
            new() { Id = 74, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Mefesutubani" },
            new() { Id = 75, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Mtsvanekvavila" },
            new() { Id = 76, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Nikea Settlement" },
            new() { Id = 77, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Ninotsminda" },
            new() { Id = 78, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Rionhesi Settlement" },
            new() { Id = 79, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Safichkhia" },
            new() { Id = 80, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Saghoria" },
            new() { Id = 81, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Ukimerioni" },
            new() { Id = 82, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Kronika" },
            new() { Id = 83, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Chavchavadze settlement" },
            new() { Id = 84, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Choma" },
            new() { Id = 85, City = "Kutaisi", Region = "Districts of Kutaisi", District = "Kopitnari" },

            /////////////////////////// RUSTAVI ///////////////////////////
            new() { Id = 86, City = "Rustavi", Region = "Districts of Rustavi", District = "New Rustavi" },
            new() { Id = 87, City = "Rustavi", Region = "Districts of Rustavi", District = "Old Rustavi" },
            new() { Id = 88, City = "Rustavi", Region = "Districts of Rustavi", District = "Tchkondideli Settlement" },

            /////////////////////////// Other Regions ///////////////////////////
            new() { Id = 89, City = "Poti", Region = "Other Regions", District = "Poti" },
            new() { Id = 90, City = "Zugdidi", Region = "Other Regions", District = "Zugdidi" },
            new() { Id = 91, City = "Telavi", Region = "Other Regions", District = "Telavi" },
            new() { Id = 92, City = "Gori", Region = "Other Regions", District = "Gori" },
            new() { Id = 93, City = "Mcxeta", Region = "Other Regions", District = "Mcxeta" },

            /////////////////////////// Suburbs Of Tbilisi ///////////////////////////
            new() { Id = 94, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Agaraki" },
            new() { Id = 95, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Akhaldaba" },
            new() { Id = 96, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Betania" },
            new() { Id = 97, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Didgori" },
            new() { Id = 98, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Didi Lilo" },
            new() { Id = 99, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Elfia" },
            new() { Id = 100, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Giorgitsminda" },
            new() { Id = 101, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Kiketi" },
            new() { Id = 102, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Kojori" },
            new() { Id = 103, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Koshigora" },
            new() { Id = 104, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Kveseti" },
            new() { Id = 105, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Napetvrebi" },
            new() { Id = 106, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Nasaguri" },
            new() { Id = 107, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Okrokana" },
            new() { Id = 108, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Shindisi" },
            new() { Id = 109, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Tabakhmela" },
            new() { Id = 110, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Telovani" },
            new() { Id = 111, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Tsavkisi" },
            new() { Id = 112, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Tsinubani" },
            new() { Id = 113, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Tskneti" },
            new() { Id = 114, City = "Georgia", Region = "Suburbs Of Tbilisi", District = "Zurgovana" },

            /////////////////////////// MUNICIPALITIES ///////////////////////////
            // A
            new() { Id = 115, City = "Georgia", Region = "Municipalities", District = "Abasha Municipality" },
            new() { Id = 116, City = "Georgia", Region = "Municipalities", District = "Abkhazia Autonomous Republic" },
            new() { Id = 117, City = "Georgia", Region = "Municipalities", District = "Abroad" },
            new() { Id = 118, City = "Georgia", Region = "Municipalities", District = "Adigeni Municipality" },
            new() { Id = 119, City = "Georgia", Region = "Municipalities", District = "Akhalgori Municipality" },
            new() { Id = 120, City = "Georgia", Region = "Municipalities", District = "Akhalkalaki Municipality" },
            new() { Id = 121, City = "Georgia", Region = "Municipalities", District = "Akhaltsikhe Municipality" },
            new() { Id = 122, City = "Georgia", Region = "Municipalities", District = "Akhmeta Municipality" },
            new() { Id = 123, City = "Georgia", Region = "Municipalities", District = "Ambrolauri Municipality" },
            new() { Id = 124, City = "Georgia", Region = "Municipalities", District = "Aspindza Municipality" },

            // B
            new() { Id = 125, City = "Georgia", Region = "Municipalities", District = "Baghdati Municipality" },
            new() { Id = 126, City = "Georgia", Region = "Municipalities", District = "Bolnisi Municipality" },
            new() { Id = 127, City = "Georgia", Region = "Municipalities", District = "Borjomi Municipality" },

            // C
            new() { Id = 128, City = "Georgia", Region = "Municipalities", District = "Chiatura Municipality" },
            new() { Id = 129, City = "Georgia", Region = "Municipalities", District = "Chokhatauri Municipality" },

            // D
            new() { Id = 130, City = "Georgia", Region = "Municipalities", District = "Dedoplistskaro Municipality" },
            new() { Id = 131, City = "Georgia", Region = "Municipalities", District = "Dmanisi Municipality" },
            new() { Id = 132, City = "Georgia", Region = "Municipalities", District = "Dusheti Municipality" },

            // G
            new() { Id = 133, City = "Georgia", Region = "Municipalities", District = "Gardabni Municipality" },
            new() { Id = 134, City = "Georgia", Region = "Municipalities", District = "Gori Municipality" },
            new() { Id = 135, City = "Georgia", Region = "Municipalities", District = "Gurjaani Municipality" },

            // J
            new() { Id = 136, City = "Georgia", Region = "Municipalities", District = "Javis Municipality" },

            // K
            new() { Id = 137, City = "Georgia", Region = "Municipalities", District = "Kareli Municipality" },
            new() { Id = 138, City = "Georgia", Region = "Municipalities", District = "Kaspi Municipality" },
            new() { Id = 139, City = "Georgia", Region = "Municipalities", District = "Kazbegi Municipality" },
            new() { Id = 140, City = "Georgia", Region = "Municipalities", District = "Keda Municipality" },
            new() { Id = 141, City = "Georgia", Region = "Municipalities", District = "Khashuri Municipality" },
            new() { Id = 142, City = "Georgia", Region = "Municipalities", District = "Khelvachauri Municipality" },
            new() { Id = 143, City = "Georgia", Region = "Municipalities", District = "Khobi Municipality" },
            new() { Id = 144, City = "Georgia", Region = "Municipalities", District = "Khoni Municipality" },
            new() { Id = 145, City = "Georgia", Region = "Municipalities", District = "Khulo Municipality" },
            new() { Id = 146, City = "Georgia", Region = "Municipalities", District = "Kobuleti Municipality" },
            new() { Id = 147, City = "Georgia", Region = "Municipalities", District = "Kvareli Municipality" },

            // L
            new() { Id = 148, City = "Georgia", Region = "Municipalities", District = "Lagodekhi Municipality" },
            new() { Id = 149, City = "Georgia", Region = "Municipalities", District = "Lanchkhuti Municipality" },
            new() { Id = 150, City = "Georgia", Region = "Municipalities", District = "Lentekhi Municipality" },

            // M
            new() { Id = 151, City = "Georgia", Region = "Municipalities", District = "Marneuli Municipality" },
            new() { Id = 152, City = "Georgia", Region = "Municipalities", District = "Martvili Municipality" },
            new() { Id = 153, City = "Georgia", Region = "Municipalities", District = "Mestia Municipality" },
            new() { Id = 154, City = "Georgia", Region = "Municipalities", District = "Mtskheti Municipality" },

            // N
            new() { Id = 155 , City = "Georgia", Region = "Municipalities", District = "Ninotsminda Municipality" },

            // O
            new() { Id = 156, City = "Georgia", Region = "Municipalities", District = "Oni Municipality" },
            new() { Id = 157, City = "Georgia", Region = "Municipalities", District = "Ozurgeti Municipality" },

            // S
            new() { Id = 158, City = "Georgia", Region = "Municipalities", District = "Sachkhere Municipality" },
            new() { Id = 159, City = "Georgia", Region = "Municipalities", District = "Sagarejo Municipality" },
            new() { Id = 160, City = "Georgia", Region = "Municipalities", District = "Samtredia Municipality" },
            new() { Id = 161, City = "Georgia", Region = "Municipalities", District = "Senaki Municipality" },
            new() { Id = 162, City = "Georgia", Region = "Municipalities", District = "Shuakhevi Municipality" },
            new() { Id = 163, City = "Georgia", Region = "Municipalities", District = "Sighnaghi Municipality" },
            new() { Id = 164, City = "Georgia", Region = "Municipalities", District = "Suburbs Of Tbilisi" },

            // T
            new() { Id = 165, City = "Georgia", Region = "Municipalities", District = "Telavi Municipality" },
            new() { Id = 166, City = "Georgia", Region = "Municipalities", District = "Terjola Municipality" },
            new() { Id = 167, City = "Georgia", Region = "Municipalities", District = "Tetri Tskaro Municipality" },
            new() { Id = 168, City = "Georgia", Region = "Municipalities", District = "Tianeti Municipality" },
            new() { Id = 169, City = "Georgia", Region = "Municipalities", District = "Tkibuli Municipality" },
            new() { Id = 170, City = "Georgia", Region = "Municipalities", District = "Tsageri Municipality" },
            new() { Id = 171, City = "Georgia", Region = "Municipalities", District = "Tsalenjikha Municipality" },
            new() { Id = 172, City = "Georgia", Region = "Municipalities", District = "Tsalka Municipality" },
            new() { Id = 173, City = "Georgia", Region = "Municipalities", District = "Tskaltubo Municipality" },

            // V
            new() { Id = 174, City = "Georgia", Region = "Municipalities", District = "Vani Municipality" },

            // Z
            new() { Id = 175, City = "Georgia", Region = "Municipalities", District = "Zestaponi Municipality" },
            new() { Id = 176, City = "Georgia", Region = "Municipalities", District = "Zugdidi Municipality" },
        };
    }
}

