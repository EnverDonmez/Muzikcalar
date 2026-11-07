# Android 1.1.1 doğrulaması — 8 Kasım 2026

- Temiz bir dizinden APK, debug ve cihaz testi paketleri üretildi. Beş JVM testi geçti. Lint sıfır hata ve 14 uyarı bildirdi.
- Ağ kapalıyken beş emülatör testi geçti. Oturum geri yükleme, yerel WAV, bozuk kaynak atlama, karışık sırada öncelikli parça, kitaplık aktarımı ve arayüz kontrol edildi.
- İki canlı emülatör testi geçti. YouTube oynatma, arka plan ve ağ geri gelince otomatik başlatma doğrulandı.
- Sürüm 1.1.1, aynı sertifikayla 1.1’in üzerine kuruldu ve emülatörde açıldı.

Fiziksel telefon ve üreticiye özgü pil yönetimi bu sürümde denenmedi.

# Android 1.1 doğrulaması — 7 Kasım 2026

- Beş JVM testi ve release lint kontrolü geçti. Lint raporunda sıfır hata var.
- Ağ kapalı emülatörde beş bütünleşme testi geçti. Yerel WAV oturumu 4.
  saniyede ve duraklatılmış olarak geri geldi. Eksik dosya sırayı silmedi.
  Karışık modda “Sırada sonraki çal” ve sırayı koruyarak “Şimdi çal” doğrulandı.
- Windows biçimindeki kitaplık içe alındı. Birleştirme tekrarlandığında kayıt
  çoğalmadı, playlist sırası kaydedildi ve geçersiz aktarım mevcut veriyi bozmadı.
- Canlı YouTube araması, ses, sarma ve arka planda çalma emülatörde geçti.
  Ağ kapalıyken başlayan istek bekledi ve bağlantı geri gelince ses otomatik başladı.
- Release APK’nın sertifikası önceki sürümle eşleşti. Emülatörde 1.0 üzerine
  kuruldu ve sürüm 1.1 olarak doğrulandı.

Fiziksel telefon, Bluetooth, kulaklık ve üreticinin pil yönetimi bu turda
test edilmedi.

# Android doğrulama — 5 Kasım 2026

## Derleme ve kaynak doğrulama

- Java 17 / Gradle 9.3.1 / Android Gradle Plugin 9.1.1 / SDK 36.
- Kişisel RSA anahtarıyla imzalı release APK; minSdk 26, targetSdk 36.
- ARM64, ARMv7 ve x86_64 tek APK içindedir.
- APK'ya eklenen yt-dlp 2026.10.26 dosyası resmi GitHub yayınındaki SHA256
  digest değeriyle birebir eşleşti. Ayrıntılar ENGINE-VERSION.json içindedir.
- APK içindeki optimize edilmiş motor kaynağının dosya özeti de doğrulandı.
  Release imzası apksigner ile geçti; temiz kurulumda açılış 1.3 saniyeydi.
- Android lint: 0 hata. Kalan öneriler çeviri kaynakları, yeni bağımlılık
  sürümleri, RecyclerView tam yenilemesi ve isteğe bağlı kaynak/simge kurallarıdır.
  R.raw.ytdlp, motor kütüphanesinin kullandığı kaynak üzerine bilinçli ektir.

## Otomatik kontroller

Üç JVM testi geçti: bağlantı doğrulama, iç içe/kaçış karakterli YouTube arama
verisini ayrıştırma, tekrar eden sonuçları ayıklama, süre ve Türkçe metin aktarımı.

API 34 emülatöründeki üç bütünleşme testi geçti:

- Playlist oluşturma/yeniden adlandırma/parça ekleme ve çıkarma; yinelenen
  isim/parça denetimi; Türkçe kayıtların yeniden yüklenmesi.
- AtomicFile yedeğinden kurtarma; bozuk kitaplığın üzerine boş veri yazmama.
- Canlı YouTube araması, tekrar arama önbelleği, gerçek ses akışını çözme,
  sesin zaman ilerlemesi, duraklatma, 15. saniyeye sarma, devam ve tekrar modu.
- Activity arka plandayken çalmanın devam etmesi.
- Beğeniler/playlist gezinmesi, sıra ekleme/çıkarma/sıralama ve liste kaydırma.

API 37, 16 KB sayfa boyutlu emülatörde canlı ses/arka plan ve kitaplık testleri de
geçti. İlk 2 GB RAM denemesi Android tarafından LOW_MEMORY nedeniyle sonlandırıldı
(uygulama Java/native hatası değildi); 4 GB RAM ile iki test de tamamlandı.

## Görsel ve etkileşim kontrolleri

- Yaklaşık 375 dp genişlikte dikey ekran; yatay ekran; sistem font ölçeği 2.0.
- Gerçek emülatör ekran görüntüleri incelendi. Büyük yazıda alt etiketler simge/
  erişilebilir açıklama biçimine geçer; yatayda başlık alanı listeye bırakılır.
- RecyclerView aşağı/yukarı kaydırma ve nav kontrollerinin en az 48 dp olması.
- Tam oynatıcının küçük ekranlarda kaydırılabilmesi ve sabit alanların sistem
  çubukları altında kalmaması. İşlemler renk/konum değişiminde yerleşimi oynatmaz.
- Koyu tema kontrastı: ana metin/zemin 17.63:1; ikincil metin/kart 7.84:1;
  birincil düğme metni/yeşil zemin 13.96:1.

Örnek emülatör aramasında mobil sayfa/yedek yöntem yaklaşık 9.4 saniye sürdü.
Masaüstü sayfa verisini doğrudan ayrıştıran yeni yol 2.36 saniyede sonuç verdi;
aynı aramanın bellek önbelleği 500 ms altı kontrolünü geçti. Bunlar tekil örnek
ölçümlerdir; bağlantı ve YouTube yanıtına göre değişir.

Fiziksel telefon, Bluetooth/kulaklık ve üretici pil yönetimi üzerinde denenmedi.
Ekran kilidi/bildirim entegrasyonu Media3 MediaSessionService ile uygulanır;
otomatik test arka plana alınmış Activity üzerinde yapıldı.
