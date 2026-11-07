# Müzikçalar 1.1.1 — Android

İmzalı sürümü derleyince üst proje klasöründe **Muzikcalar.apk** oluşur.
Telefona kopyalayıp aç. Android sorarsa kullandığın dosya yöneticisi için
“Bu kaynaktan yüklemeye izin ver” seçeneğini aç. Android 8.0 veya üzeri gerekir.
APK kişisel anahtarla imzalanır; mağazaya gönderilmez.

## Kullanım

- Keşfet'te şarkı/sanatçı ara veya bir YouTube video bağlantısı yapıştır.
  YouTube uygulamasının Paylaş menüsünden de Müzikçalar'a bağlantı gönderebilirsin.
- Bir sonuca dokununca parça çalar ve mevcut sıra korunur. Üç nokta menüsünden
  sırada sonraki çalabilir, sıranın sonuna ekleyebilir ya da o parçadan listeyi
  başlatabilirsin. Menüden ayrıca beğenebilir ve playlist’e ekleyebilirsin.
- Alt menüde beğeniler, playlistler, çalma sırası ve son dinlenenler bulunur.
- Playlistler’de “+ Yeni” ile liste oluştur. Liste başlığından arama, sıralama,
  yeniden adlandırma ve silme işlemleri yapılır. Parça menüsünden parçayı taşı
  veya listeden çıkar.
- Mini oynatıcıya dokununca tam oynatıcı açılır: kapak, ileri sarma,
  önceki/sonraki, duraklatma, durdurma, tekrar, karışık çalma ve ses seviyesi.
- Tekrar düğmesi kapalı → aynı parça → tüm sıra döngüsünde değişir.
- Sıra ekranındaki şarkı menüsünden kaldırabilir veya yukarı/aşağı taşıyabilirsin.
- Müzik ekran kapalıyken/başka uygulamadayken çalar. Bildirim ve kilit ekranından
  kontrol edebilirsin. Bazı telefonlarda üreticinin pil kısıtlamasını kaldırmak
  gerekebilir. Kulaklık çıkarılınca ses duraklatılır; aramalarda ses odağı korunur.
- Ayarlar’dan cihazındaki ses dosyalarını açabilir, kitaplığını içe/dışa
  aktarabilir, ses motorunu güncelleyebilir ve sürümünü görebilirsin.
  İlk ses açılışı motor hazırlanırken
  sonraki açılışlardan daha uzun sürebilir. Aramalar 10 dakika, ses bağlantıları
  4 dakika önbellekte tutulur; kapaklar yerel ağ önbelleğini kullanır.

Beğeniler ve playlistler uygulamanın özel alanında kalıcı saklanır. JSON
dışa aktarımı Windows’ta da açılır ve içe aktarılan kayıtlar mevcut kayıtlarla
birleştirilir. Cihaza bağlı yerel ses dosyaları aktarım dosyasına eklenmez.
Uygulamayı kaldırmadan önce kitaplığını dışa aktarabilirsin.
Çalma sırası, ses, tekrar, karışık sıra ve son konum korunur. Yeniden açılışta
oturum duraklatılmış halde gelir. Geçici ağ hatasında uygulama bağlantıyı
yeniler ve son konumdan devam etmeyi dener. Uygulama hesap, API anahtarı veya
bilgisayarda çalışan sunucu gerektirmez.

YouTube'daki giriş/yaş/bölge kısıtlı içerikler desteklenmez. Kaydın içindeki
sponsor bölümleri kesilmez. YouTube çalma internet erişimi gerektirir. YouTube yöntem değiştirirse
ses motorunu güncellemek veya uygulamayı yeniden derlemek gerekebilir.

## Derleme

Java 17+, Android SDK 36 ve Gradle 9.3.1 kullanılır. Bu bilgisayarda SDK ve Java
mevcuttur. `android\Derle-Android.ps1` APK'yı yeniden oluşturur.
Android Studio ile de `android` klasörünü açabilirsin. Başka bilgisayarda
`local.properties` içindeki SDK yolunu değiştir.

İmzalama anahtarı ve `release.properties` bu bilgisayarda
`%LOCALAPPDATA%\Muzikcalar\signing` dizinindedir. Başka bir dizin için
`MUZIKCALAR_SIGNING_PROPERTIES` değişkenini kullan. Ayar dosyası `storeFile`,
`storePassword`, `keyAlias` ve `keyPassword` alanlarını içerir. Anahtar ve
ayar dosyasını özel olarak yedekle. Mevcut APK’nın üzerine kurabilmek için aynı
anahtar gerekir. Debug derlemesi özel imzalama ayarlarına ihtiyaç duymaz.

## Bileşenler

- [AndroidX Media3](https://github.com/androidx/media) — Apache 2.0.
- [youtubedl-android 0.18.1](https://github.com/yausername/youtubedl-android) — GPLv3;
  yt-dlp, Python ve QuickJS bileşenlerini içerir. Ses akışı çözülür, video oynatılmaz.
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) — Unlicense; paket bağımlılıklarının ayrı lisansları vardır.
- [OkHttp](https://github.com/square/okhttp) — Apache 2.0.
- [Montserrat](https://github.com/JulietaUla/Montserrat) — SIL OFL; font lisansı APK içinde bulunur.

Uygulamanın Android kaynakları `app/src/main`, testleri `app/src/test` ve
`app/src/androidTest` içindedir. Kişisel kullanım için hazırlanmıştır.
