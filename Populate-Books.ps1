param(
    [string]$ProxyUrl = "http://localhost:9000/api/book"
)

$books = @(
    @{ title = "Lolita";                      author = "Vladimir Nabokov";       publishedYear = 1955; pages = 336; genre = "roman" },
    @{ title = "Strainul";                    author = "Albert Camus";           publishedYear = 1942; pages = 159; genre = "roman" },
    @{ title = "Maitreyi";                    author = "Mircea Eliade";          publishedYear = 1933; pages = 240; genre = "roman" },
    @{ title = "Ion";                         author = "Liviu Rebreanu";         publishedYear = 1920; pages = 400; genre = "roman" },
    @{ title = "Razboi si pace";              author = "Lev Tolstoi";            publishedYear = 1869; pages = 1225; genre = "istorie" },
    @{ title = "Procesul";                    author = "Franz Kafka";            publishedYear = 1925; pages = 255; genre = "roman" },
    @{ title = "1984";                        author = "George Orwell";          publishedYear = 1949; pages = 328; genre = "distopie" },
    @{ title = "Eseu despre orbire";          author = "Jose Saramago";          publishedYear = 1995; pages = 352; genre = "roman" },
    @{ title = "Luceafarul";                  author = "Mihai Eminescu";         publishedYear = 1883; pages = 64;  genre = "poezie" },
    @{ title = "Baltagul";                    author = "Mihail Sadoveanu";       publishedYear = 1930; pages = 200; genre = "roman" },
    @{ title = "Crima si pedeapsa";           author = "Feodor Dostoievski";     publishedYear = 1866; pages = 671; genre = "roman" },
    @{ title = "Maestrul si Margareta";       author = "Mihail Bulgakov";        publishedYear = 1967; pages = 480; genre = "fantasy" },
    @{ title = "Un veac de singuratate";      author = "Gabriel Garcia Marquez"; publishedYear = 1967; pages = 417; genre = "roman" },
    @{ title = "Mandrie si prejudecata";      author = "Jane Austen";            publishedYear = 1813; pages = 432; genre = "roman" },
    @{ title = "Micul print";                 author = "Antoine de Saint-Exupery"; publishedYear = 1943; pages = 96; genre = "poveste" },
    @{ title = "Moara cu noroc";              author = "Ioan Slavici";           publishedYear = 1881; pages = 110; genre = "nuvela" },
    @{ title = "Enigma Otiliei";              author = "George Calinescu";       publishedYear = 1938; pages = 420; genre = "roman" },
    @{ title = "Padurea spanzuratilor";       author = "Liviu Rebreanu";         publishedYear = 1922; pages = 300; genre = "roman" },
    @{ title = "Fratii Karamazov";            author = "Feodor Dostoievski";     publishedYear = 1880; pages = 824; genre = "roman" },
    @{ title = "Hobbitul";                    author = "J.R.R. Tolkien";         publishedYear = 1937; pages = 310; genre = "fantasy" },
    @{ title = "Fahrenheit 451";              author = "Ray Bradbury";           publishedYear = 1953; pages = 249; genre = "distopie" },
    @{ title = "Numele trandafirului";        author = "Umberto Eco";            publishedYear = 1980; pages = 536; genre = "politist" },
    @{ title = "Ghepardul";                   author = "Giuseppe Tomasi di Lampedusa"; publishedYear = 1958; pages = 320; genre = "roman" },
    @{ title = "Batranul si marea";           author = "Ernest Hemingway";       publishedYear = 1952; pages = 127; genre = "roman" },
    @{ title = "Doamna Bovary";               author = "Gustave Flaubert";       publishedYear = 1857; pages = 464; genre = "roman" },
    @{ title = "Calatorie spre centrul pamantului"; author = "Jules Verne";      publishedYear = 1864; pages = 320; genre = "aventura" },
    @{ title = "Dune";                        author = "Frank Herbert";          publishedYear = 1965; pages = 612; genre = "science-fiction" },
    @{ title = "Ghidul autostopistului galactic"; author = "Douglas Adams";      publishedYear = 1979; pages = 224; genre = "science-fiction" },
    @{ title = "Poezii";                      author = "Nichita Stanescu";       publishedYear = 1966; pages = 180; genre = "poezie" },
    @{ title = "Ultima noapte de dragoste, intaia noapte de razboi"; author = "Camil Petrescu"; publishedYear = 1930; pages = 390; genre = "roman" }
)

$ok = 0
$fail = 0
$total = $books.Count

for ($i = 0; $i -lt $total; $i++) {
    $book  = $books[$i]
    $json  = $book | ConvertTo-Json
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)

    try {
        Invoke-RestMethod -Uri $ProxyUrl -Method Post -Body $bytes `
            -ContentType "application/json; charset=utf-8" | Out-Null
        $ok++
        Write-Host "[$($i + 1)/$total] OK   $($book.title) - $($book.author)"
    }
    catch {
        $fail++
        $status = $_.Exception.Response.StatusCode.value__
        Write-Host "[$($i + 1)/$total] EROARE ($status) $($book.title)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Gata: $ok reusite, $fail esuate." -ForegroundColor Cyan