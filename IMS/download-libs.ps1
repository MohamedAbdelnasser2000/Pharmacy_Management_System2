# Download Frontend Libraries for Offline Use
Write-Host "Downloading libraries..." -ForegroundColor Cyan

$libPath = "wwwroot/lib"

# Create directories
New-Item -ItemType Directory -Force -Path "$libPath/bootstrap/css" | Out-Null
New-Item -ItemType Directory -Force -Path "$libPath/bootstrap/js" | Out-Null
New-Item -ItemType Directory -Force -Path "$libPath/fontawesome/css" | Out-Null
New-Item -ItemType Directory -Force -Path "$libPath/fontawesome/webfonts" | Out-Null
New-Item -ItemType Directory -Force -Path "$libPath/adminlte/css" | Out-Null
New-Item -ItemType Directory -Force -Path "$libPath/adminlte/js" | Out-Null
New-Item -ItemType Directory -Force -Path "$libPath/chartjs" | Out-Null
New-Item -ItemType Directory -Force -Path "$libPath/toastr" | Out-Null
New-Item -ItemType Directory -Force -Path "$libPath/fonts/tajawal" | Out-Null

# Download Bootstrap
Write-Host "Downloading Bootstrap..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.rtl.min.css" -OutFile "$libPath/bootstrap/css/bootstrap.rtl.min.css"
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" -OutFile "$libPath/bootstrap/css/bootstrap.min.css"
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js" -OutFile "$libPath/bootstrap/js/bootstrap.bundle.min.js"

# Download Font Awesome
Write-Host "Downloading Font Awesome..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.2/css/all.min.css" -OutFile "$libPath/fontawesome/css/all.min.css"
Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.2/webfonts/fa-solid-900.woff2" -OutFile "$libPath/fontawesome/webfonts/fa-solid-900.woff2"
Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.2/webfonts/fa-regular-400.woff2" -OutFile "$libPath/fontawesome/webfonts/fa-regular-400.woff2"
Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.2/webfonts/fa-brands-400.woff2" -OutFile "$libPath/fontawesome/webfonts/fa-brands-400.woff2"

# Download AdminLTE
Write-Host "Downloading AdminLTE..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/admin-lte@3.2/dist/css/adminlte.min.css" -OutFile "$libPath/adminlte/css/adminlte.min.css"
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/admin-lte@3.2/dist/js/adminlte.min.js" -OutFile "$libPath/adminlte/js/adminlte.min.js"

# Download Chart.js
Write-Host "Downloading Chart.js..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js" -OutFile "$libPath/chartjs/chart.min.js"

# Download Toastr
Write-Host "Downloading Toastr..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/toastr.js/2.1.4/toastr.min.js" -OutFile "$libPath/toastr/toastr.min.js"
Invoke-WebRequest -Uri "https://cdnjs.cloudflare.com/ajax/libs/toastr.js/2.1.4/toastr.min.css" -OutFile "$libPath/toastr/toastr.min.css"

# Create Tajawal font CSS
Write-Host "Creating font CSS..." -ForegroundColor Yellow
$fontCSS = "@font-face { font-family: 'Tajawal'; font-weight: 400; src: url('https://fonts.gstatic.com/s/tajawal/v9/Iura6YBj_oCad4k1nzSBC45I.woff2') format('woff2'); }"
Set-Content -Path "$libPath/fonts/tajawal/tajawal.css" -Value $fontCSS

Write-Host "Download complete!" -ForegroundColor Green
