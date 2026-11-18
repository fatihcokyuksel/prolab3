# Staj Evrak Doğrulama ve Yönetim Sistemi

Kocaeli Üniversitesi Yazılım Mühendisliği, Programlama Laboratuvarı III (2026-2027 Güz) projesi, vize aşaması.

Geliştiren: Fatih Çokyüksel (260229086)

Öğrenci Ek-16 Staj Kabul Formu'ndaki bilgileri sisteme girer ve imzalı formu yükler. Sistem dosyayı kontrol eder, Word ve metin tabanlı PDF belgelerindeki alanları okur, bunları öğrencinin girdiği bilgilerle ve admin'in yüklediği okul veri setiyle karşılaştırır. Uyuşmayan veya boş alanlar formun üzerinde gösterilir. Admin başvuruları inceler, onaylar, tamamlar ve kullanıcı hesaplarını yönetir.

## Vize beklentileri

| Beklenti | Karşılığı |
|---|---|
| UML, akış, veri tabanı ve class diyagramları | `docs/diagrams/` altında dört grup (aşağıdaki proje yapısına bakın) |
| Oturum açma ve yetkilendirme | Öğrencinin kendi kaydı, kullanıcı adı veya e-posta ile giriş, PBKDF2 şifre özeti, 5 hatalı denemede 15 dakika kilit, IP bazlı deneme sınırı, giriş logları, rol bazlı erişim (ROLE_STUDENT / ROLE_ADMIN), yetkisiz istekte 403 sayfası ve login'e yönlendirme, çıkışta oturumun geçersiz kılınması |
| Admin ve öğrenci sayfaları | Admin: panel, başvurular (statü sekmeleri, filtre, ayrı "Tamamlandı" sekmesi), veri seti, kullanıcılar. Öğrenci: ana sayfa, Ek-16 bilgilerim, evrak yükle, kontrol raporu |
| Admin'de veri seti yükleme | .xlsx veya .csv. Kayıtlar TC kimlik no'ya göre eklenir/güncellenir, hatalı satırlar satır numarasıyla raporlanır, kayıtlı öğrenciler öğrenci no veya e-posta ile otomatik eşleştirilir |
| NYP prensipleri | Aşağıdaki tabloya ve `docs/diagrams/4-sinif/` klasöründeki sınıf diyagramlarına bakın |
| Evrak yükleme ve kontrolleri | Uzantı (.doc, .docx, .pdf, .jpg, .jpeg, .png), boyut (en fazla 10 MB) ve dosya imzası kontrolü (uzantısı değiştirilmiş dosyalar reddedilir), hata durumunda pop-up uyarı, belge sürümleri |

Bunlara ek olarak:

- Ek-16 bilgileri kaydedilmeden önce doğrulanır. TC kimlik no uyruğa göre kontrol edilir (T.C.: 11 hane, ilk hane 0 olamaz, kontrol haneleri; yabancı uyruklu: 11 hane, 99 ile başlar). Telefonlar ülke kodu seçilerek tek standartta (`+90 532 123 45 67`) saklanır.
- Admin kullanıcı hesaplarını düzenleyebilir, şifre sıfırlayabilir, kilit açabilir, oturumları kapatabilir ve öğrenci hesabını başvurusu ve belgeleriyle birlikte silebilir. Admin hesapları silinemez.

Final aşamasına kalanlar: OCR / el yazısı tanıma, imza ve kaşe kontrolü, dokümanın 6. bölümündeki tarih ve staj günü kuralları, Google Form'a kayıt, Ek-17 üretimi.

## Kullanılan teknolojiler

.NET 10, ASP.NET Core MVC, Entity Framework Core + SQLite, Bootstrap 5, DocumentFormat.OpenXml (Word okuma), PdfPig (PDF okuma), ClosedXML (Excel), CsvHelper, PlantUML (diyagramlar). Hepsi ücretsizdir. Arayüz yazı tipi (Public Sans) proje içinde gelir, çalışmak için internet gerekmez.

## Çalıştırma

Gereken tek şey [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet run --project src/StajEvrak.Web --launch-profile http
```

Tarayıcıda http://localhost:5021 adresini açın.

Proje boş teslim edilir. Veritabanı (`src/StajEvrak.Web/App_Data/stajevrak.db`) ilk çalıştırmada otomatik oluşturulur ve içine yalnızca admin hesabı eklenir; öğrenci, veri seti ve başvuru kaydı yoktur. Yüklenen belgeler `src/StajEvrak.Web/App_Data/uploads/` altında saklanır. Sistemi yeniden sıfırlamak için uygulama kapalıyken `src/StajEvrak.Web/App_Data` klasörünü silmek yeterlidir.

| Hesap | Kullanıcı adı | Şifre |
|---|---|---|
| Admin | `admin` | `Admin12345` (`appsettings.json` → `Seed`) |
| Öğrenci | "Kayıt ol" sayfasından oluşturulur | en az 8 karakter, harf ve rakam |

## Demo senaryosu

1. `admin` ile giriş yapın. **Veri seti** sayfasından `samples/test-dataset.xlsx` dosyasını yükleyin: 20 kayıt eklenir, 4 hatalı satır (eksik ad, 10 haneli TC, geçersiz tarih, tekrar eden TC) raporlanır.
2. Çıkış yapıp **Kayıt ol** sayfasına gidin. Öğrenci no `220100001`, e-posta `ali.yilmaz@kocaeli.edu.tr` ile kaydolun. Hesap veri setindeki "Ali Yılmaz" kaydıyla otomatik eşleşir.
3. Öğrenci olarak `/Admin` adresine gidin. 403 sayfası açılır ve login'e yönlendirilirsiniz.
4. **Ek-16 bilgilerim** sayfasından bilgileri girin. Hatalı TC kimlik no veya eksik haneli telefon girildiğinde alanın altında hata gösterilir ve kayıt yapılmaz.
5. **Evrak yükle** sayfasında sırayla deneyin:
   - `samples/not.txt` → biçim kabul edilmez
   - `samples/sahte-belge.pdf` → içerik uzantıyla uyuşmaz (aslında metin dosyası)
   - `samples/ek16-hatali.docx` → raporda TC, bölüm ve doğum tarihi uyuşmazlığı, 2 boş zorunlu alan
   - `samples/ek16-dogru.pdf` → uyumlu rapor
6. Admin olarak **Başvurular** sayfasından başvuruyu açın, önce **Onayla** sonra **Tamamla** deyin. Başvuru "Tamamlandı" sekmesine geçer. Öğrenci artık belge yükleyemez ve silemez; admin silebilir ve yeniden açabilir.
7. Yanlış şifreyle 5 kez giriş deneyin; hesap 15 dakika kilitlenir. Admin **Kullanıcılar** sayfasından kilidi açabilir ve giriş loglarını görebilir. Kullanıcının adına tıklanınca hesap düzenleme, şifre sıfırlama, oturum kapatma ve hesap silme işlemleri yapılır.

Örnek dosyaları yeniden üretmek için: `dotnet run tools/GenerateSamples.cs` (şablon olarak `samples/ek16-sablon.docx` kullanılır).

## Proje yapısı

```
src/StajEvrak.Domain/          Varlıklar (kullanıcı, başvuru, belge, veri seti kaydı), statü (State) sınıfları,
                               Ek-16 alan kataloğu. Hiçbir kütüphaneye bağımlı değildir.
src/StajEvrak.Application/     Servisler (giriş, yükleme, karşılaştırma, veri seti, hesap yönetimi), arayüzler,
                               dosya imzaları, alan karşılaştırıcıları ve doğrulayıcıları
src/StajEvrak.Infrastructure/  EF Core (AppDbContext, migration, repository), Word/PDF okuyucuları,
                               Excel/CSV ayrıştırıcıları, dosya deposu
src/StajEvrak.Web/             MVC: Account, Areas/Admin, Areas/Student, görünümler, CSS/JS
samples/                       Demo dosyaları (veri seti, boş Ek-16 şablonu, doğru/hatalı Ek-16, sahte ve geçersiz dosyalar)
docs/diagrams/                 PlantUML kaynakları (.puml) ile PNG ve SVG çıktıları
  1-uml/                       Kullanım senaryosu, sıralama ve durum diyagramları
  2-akis/                      Giriş ve evrak yükleme akış diyagramları
  3-veritabani/                Varlık-ilişki (ER) diyagramı
  4-sinif/                     Sınıf diyagramları (kullanıcılar ve State, evrak, doğrulama)
  _style.iuml                  Diyagramların ortak stili
tools/GenerateSamples.cs       Örnek dosya üreticisi
```

## NYP prensipleri: kodda nerede?

| Prensip | Örnek |
|---|---|
| **Kapsülleme** | `User` sınıfında şifre özeti, hatalı deneme sayacı ve kilit alanları `private set`; yalnızca `SetPasswordHash`, `RegisterFailedLogin` gibi metotlarla değişir. `InternshipApplication.Status` dışarıdan atanamaz; `Approve()`, `MarkInvalid()`, `Complete()` kullanılır. Belge listesi dışarıya salt okunur verilir. |
| **Soyutlama** | `abstract User`, `ApplicationState`, `FileSignature`, `DocumentReader`, `DatasetParser`, `FieldComparer`, `FieldValidator`; `IDocumentReader` ve repository arayüzleri |
| **Kalıtım** | `User` → `Student`, `Admin` (veritabanında tek tablo, TPH); `ApplicationState` → 6 statü sınıfı; `FileSignature` → PDF/DOCX/DOC/JPEG/PNG; `DocumentReader` → Docx/Pdf/Image/LegacyDoc okuyucuları; `DatasetParser` → Excel/CSV; `AdminControllerBase`, `StudentControllerBase` |
| **Çok biçimlilik** | `user.CanAccess(app)`: öğrenci yalnızca kendi başvurusuna, admin hepsine erişir. `state.CanBeModifiedBy(user)`: Tamamlandı'da öğrenci hayır, admin evet. `user.CanBeDeletedBy(admin)`: öğrenci hesabı silinebilir, admin hesabı silinemez. `DocumentReaderFactory`, `FieldComparer.For` ve `FieldValidator.For` türe göre uygun nesneyi seçer. |
| **Tasarım desenleri** | State (statü akışı), Template Method (`DatasetParser.Parse`, `DocumentReader.Read`), Factory (`DocumentReaderFactory`, `FieldComparer.For`, `FieldValidator.For`), Repository + Unit of Work |

## Diyagramları yeniden üretme

Diyagramların PNG ve SVG halleri klasörde hazırdır; bu adım yalnızca bir `.puml` dosyası değiştirildiğinde gerekir. Java ve PlantUML'in MIT sürümü yeterlidir (yerleşik smetana motoru kullanıldığı için Graphviz gerekmez).

```bash
curl -L -o tools/plantuml.jar https://github.com/plantuml/plantuml/releases/download/v1.2026.10/plantuml-mit-1.2026.10.jar
cd docs/diagrams
java -jar ../../tools/plantuml.jar -charset UTF-8 -tpng */*.puml
java -jar ../../tools/plantuml.jar -charset UTF-8 -tsvg */*.puml
```

VS Code'daki "PlantUML" eklentisiyle de önizlenebilir.
