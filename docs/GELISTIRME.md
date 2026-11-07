# Müzikçalar geliştirme kılavuzu

Müzikçalar, Windows için taşınabilir bir EXE ve Android için imzalı bir APK olarak hazırlanır. İki uygulama aynı marka varlıklarını ve kitaplık aktarım biçimini paylaşır.

| Konum | Sorumluluk |
| --- | --- |
| src/Core/ | Windows parça ve kitaplık modelleri, aktarım kuralları |
| src/Infrastructure/ | Dosya yolları ve kayıt, arama, yt-dlp, mpv bağlantısı, kapaklar |
| src/UI/ | WinForms arayüzü, oynatma akışı, oturum ve kitaplık işlemleri |
| src/Diagnostics/ ve tests/ | Windows doğrulama komutları ve sabit test örnekleri |
| android/app/src/ | Android kaynakları, JVM testleri ve cihaz testleri |
| assets/ | Ortak logo ve yazı tipleri, Windows simgesi |
| bin/ | Windows dağıtımı için mpv, yt-dlp ve çalışma bağımlılığı |
| data/ | Windows oturumu, kitaplık ve yeniden oluşturulabilen önbellek |
| docs/ | Geliştirme belgeleri ve test kayıtları |
| artifacts/ | Android derleme çıktıları ve Windows test raporları |

Windows’ta MainForm kullanıcı eylemlerini işler ve mpv olaylarını gösterir. AudioPlayer süreç ve IPC bağlantısını, YouTube araçları arama ve akış çözümünü yönetir. Android’de MainActivity arayüzü yönetir. PlaybackService çalma sırası ve ağdan toparlanmayı, PlaybackSessionStore oturumu, MusicDocuments cihaz dosyalarını, CoverLoader kapakları yönetir.

## Windows

Proje kökünde bir `bin/` klasörü oluştur. [mpv Windows derlemesindeki](https://github.com/shinchiro/mpv-winbuild-cmake/releases) `mpv.exe` dosyasını ve arşivde varsa `d3dcompiler_43.dll` dosyasını buraya çıkar. [yt-dlp sürümlerinden](https://github.com/yt-dlp/yt-dlp/releases) Windows için `yt-dlp.exe` dosyasını da aynı klasöre koy. Ardından proje kökünde PowerShell ile çalıştır.

```powershell
.\Derle.ps1
.\tests\Windows-Dogrula.ps1
.\tests\Windows-Dogrula.ps1 -Canli
```

Doğrulama, arama ayrıştırma, kitaplık, yerel mpv oynatma ve WinForms yerleşim kontrollerini çalıştırır. `-Canli` YouTube arama ve ses denemesini ekler. Raporlar varsayılan olarak `artifacts/` altında oluşur. EXE’yi değiştirirken uygulamayı kapat.

## Android

Java 17 ve Android SDK 36 gerekir. SDK yolunu Android Studio, `ANDROID_HOME` veya makineye özel `android/local.properties` ile belirt.

İmzalama anahtarı gerektirmeyen debug APK için Android klasöründe `.\gradlew.bat :app:assembleDebug` çalıştır. Android sürümü, Gradle bağımlılığının sunduğu yt-dlp ile derlenebilir. Motor sürümünü uygulamanın Ayarlar menüsünden güncelleyebilirsin.

```powershell
.\android\Derle-Android.ps1
Push-Location android
.\gradlew.bat :app:testDebugUnitTest :app:lintRelease
.\gradlew.bat :app:connectedDebugAndroidTest
Pop-Location
```

İmzalama ayarları için [Android kullanım ve derleme notlarına](../android/OKU.md) bak. `-Offline` seçeneği daha önce önbelleğe alınan Gradle bağımlılıklarını kullanır. `gradlew clean` derleme çıktılarını temizler.

Windows oturum dosyası ve Android cihaz kitaplığı kullanıcının verisidir. Yayın öncesi Windows EXE ile gerekli `bin/` ve `assets/` klasörlerini birlikte doğrula. Android APK’nın aynı anahtarla imzalandığını ve önceki sürümün üstüne kurulduğunu kontrol et.

[Windows test kayıtları](windows/TEST-SONUCLARI.txt) · [Android test kayıtları](android/TEST-SONUCLARI.md) · [Bileşenler](BILESENLER.txt)
