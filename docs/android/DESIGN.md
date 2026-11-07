# Müzikçalar Android

UI/UX Pro Max ile `music player mobile dark --design-system` sorgusu çalıştırıldı.
Dark Mode (OLED) sonucu bu ürünle uyumlu bulundu. Pazarlama sayfası bölümleri
oynatıcıya uygun olmadığı için kullanılmadı. `bottom navigation music player`
ve daraltılmış `bottom-nav-limit` aramaları web odaklı sonuçlar verdi; mobil
gezinmede skill'in `pro-rules.md` ve `quick-reference.md` yerel genel kuralları
kullanıldı. Android uygulaması bu proje için yerel Java/Views ile oluşturuldu;
WinForms'un masaüstü yerleşimi telefona taşınmadı.

Kullanıcının onayladığı kimlik önceliklidir: koyu zemin, açık yeşil vurgu,
Montserrat ExtraBold/Regular ve değiştirilmemiş renkli M logosu.

- Beş alt sekme var. Keşfet, Beğeniler, Playlistler, Sıra ve Geçmiş.
- İçerikten ayrı yer kaplayan sabit mini oynatıcı; açılabilir tam ekran oynatıcı.
- Android sistem çubukları, ekran kesikleri ve klavye için pencere inset'leri.
- 48 dp etkileşim alanları, native ripple, metin alternatifli vektör kontroller.
- RecyclerView ile geri dönüştürülen satırlar; kapaklar arka planda ve önbellekli.
- Sabit kapak boyutları, satır taşıması, tam parça bilgisi menüsü.
- Kısa/yatay ekranlarda kısaltılmış başlık ve simgeli alt gezinme.
- Tam oynatıcı kontrolleri kısa ekranlarda dikey kaydırılır.
- Metinler `sp` ile sistem yazı boyutunu izler; satırlar içerikle büyür.
- Özel sürekli animasyon kullanılmaz; platformun azaltılmış hareket tercihi izlenir.

Koyu tema kontrastları bağımsız hesaplanır; açık tema kullanıcı isteği değildir.
Gerçek cihaz testi yapılmadı; test kapsamı TEST-SONUCLARI.md içinde belirtilir.
