# Müzikçalar arayüz düzeltmesi

Kapsam: mevcut Windows Forms uygulamasının çizim ve yerleşim sorunları.
Koyu tema, Montserrat ExtraBold/Regular ve YouTube kapakları korundu.

## Uygulanan rehber

[UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill)
Arayüz incelemesinde bu rehberin erişilebilirlik ve etkileşim kuralları kullanıldı.

Mevcut bileşen hatası için yalnızca ilgili UX sorguları çalıştırıldı;
bir web veya WPF stack'i varsayılmadı. Skill'de WinForms bölümü yok.

- `text truncation readable`: uzun adlara tam erişim ve metin taşmasını
  önleme önerileri bu arayüz için uygun bulundu.
- Düğme durumu aramaları birebir masaüstü çizim sorunu eşleşmesi vermedi.
  Bu konuda `references/quick-reference.md` içindeki genel `press-feedback`,
  `state-clarity`, `focus-states` ve `truncation-strategy` kuralları kullanıldı.

## Düzeltmeler

- Yuvarlatılmış düğmelerin tüm yüzeyi her yeniden çizimde temizlenir.
  Windows'un varsayılan düğme arka planı özel çizime karışmaz.
- Normal, hover, basılı, klavye odağı, etkin ve devre dışı durumlar ayrıdır.
  Durum değişimi düğmenin konumunu veya boyutunu değiştirmez.
- Alt oynatma kontrolleri eşit hit alanlarına ve boşluklara sahiptir.
  Klavye odağı görünür; etkin tekrar durumunda ayrıca işaret bulunur.
- Parça adı sağ panelde satırlara bölünür. Tam parça bilgisi ayrı bir
  düğmeyle açılır; bu yol klavyeyle de erişilebilirdir.
- Gezinme düğmesinin etiketi kuyruk sayısı arttığında kesilmez.
- Kaydırıcı çizgisi ve odak çerçevesinin koyu temadaki kontrastı artırıldı.

## Doğrulama

`tests/Windows-Dogrula.ps1` içindeki görsel doğrulama:

- 144 düğme durumu/boyut/ölçek bileşiminin köşe pikselleri kontrol edilir.
- Metin, birincil eylem, kaydırıcı ve odak kontrastı ölçülür.
- %100, %150 ve %200 ölçeklerde tam pencere örnekleri oluşturulur.
- Pencere düzeni, fare tekerleği yönlendirmesi ve çalma sırası kontrolleri
  aynı ölçeklerde çalıştırılır.

Kontroller Windows masaüstü kapsamına göre uygulandı; mobil güvenli alan
ve web framework kuralları bu uygulamaya doğrudan aktarılmadı.

## Kitaplık ve hover kararlılığı

- Owner-draw listeler `UserPaint + AllPaintingInWmPaint + OptimizedDoubleBuffer`
  ile tek karede çizilir; native ara arka plan temizlemesi engellenir.
- Hover ve kapak güncellemeleri yalnızca ilgili satırları geçersiz kılar.
- Beğendiklerim ve Playlistlerim kalıcı sol gezinmeye eklendi.
- Yeni playlist, playlist’e ekleme, kalple beğenme ve listeyi çalma görünür
  eylemlerdir. Playlist adları ve kayıtlı parçalara klavyeyle erişilebilir.
- JSON kitaplık kaydı atomik değiştirilir; önceki sürüm yedeklenir. Okunamayan
  kitaplığın üzerine boş kayıt yazılmaz. Testler üretim kitaplığını kullanmaz.
- UI testleri 30 hover karesinde kapakların ve liste konumunun korunmasını,
  playlist/beğeni gezinmesini, liste çalma sırasını ve küçük pencere düzenini denetler.

## Onaylanan logo

- Onaylanan renkli M görseli ortak `assets/branding/res/drawable-nodpi/brand_logo.png`
  dosyasından yüklenir. Sol üstte uygulama adıyla birlikte gösterilir.
- Pencere, çalıştırılabilir dosya ve masaüstü kısayolu aynı tasarımın 16–256 piksel
  boyutlarını içeren simgesini kullanır.
- Logo eklendikten sonra normal/küçük pencere düzeni, Montserrat yazı tipleri,
  kaydırma, hover ve kitaplık arayüz kontrolleri yeniden geçti.
