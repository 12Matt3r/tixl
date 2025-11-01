#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Code Signing Certificate Management Tool
.DESCRIPTION
    Manages code signing certificates for TiXL artifacts.
    Provides functionality to create, import, export, and verify certificates.
.PARAMETER Action
    Action to perform: Create, Import, Export, List, Verify, Remove
.PARAMETER CertificatePath
    Path to certificate file (.pfx, .cer)
.PARAMETER CertificateStore
    Certificate store to use (Cert:\CurrentUser\My, Cert:\LocalMachine\My)
.PARAMETER CertificateThumbprint
    Certificate thumbprint for verification/removal
.PARAMETER Password
    Password for certificate file
.PARAMETER OutputPath
    Output path for certificate operations
.EXAMPLE
    .\scripts\manage-certificates.ps1 -Action List
.EXAMPLE
    .\scripts\manage-certificates.ps1 -Action Create -OutputPath "certificates\tixl-signing.pfx" -Password "secure-password"
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Create", "Import", "Export", "List", "Verify", "Remove", "Info")]
    [string]$Action,
    
    [Parameter(Mandatory = $false)]
    [string]$CertificatePath = "",
    
    [Parameter(Mandatory = $false)]
    [string]$CertificateStore = "Cert:\CurrentUser\My",
    
    [Parameter(Mandatory = $false)]
    [string]$CertificateThumbprint = "",
    
    [Parameter(Mandatory = $false)]
    [string]$Password = "",
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "",
    
    [Parameter(Mandatory = $false)]
    [int]$ValidityDays = 365,
    
    [Parameter(Mandatory = $false)]
    [string]$Subject = "CN=TiXL Code Signing,O=TiXL Project,C=US",
    
    [Parameter(Mandatory = $false)]
    [switch]$ExportPrivateKey = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$IncludeChain = $false
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "TiXL Code Signing Certificate Manager" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Action: $Action" -ForegroundColor Yellow

function Write-CertLog {
    param([string]$Message, [string]$Level = "INFO")
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    switch ($Level) {
        "ERROR" { Write-Host $logMessage -ForegroundColor Red }
        "WARN" { Write-Host $logMessage -ForegroundColor Yellow }
        "INFO" { Write-Host $logMessage -ForegroundColor Green }
        "DEBUG" { Write-Host $logMessage -ForegroundColor Magenta }
    }
}

function New-SelfSignedCertificate {
    param([string]$Subject, [int]$ValidityDays, [string]$OutputPath, [string]$Password)
    
    Write-CertLog "Creating self-signed certificate for code signing..." "INFO"
    
    # Create certificate with enhanced key usage for code signing
    $certParams = @{
        Subject = $Subject
        Type = "CodeSigning"
        KeyExportPolicy = "Exportable"
        KeySpec = "Signature"
        KeyLength = 4096
        HashAlgorithm = "SHA256"
        NotAfter = (Get-Date).AddDays($ValidityDays)
        CertStoreLocation = "Cert:\CurrentUser\My"
        FriendlyName = "TiXL Code Signing Certificate"
        TextExtension = @(
            "2.5.29.37={text}1.3.6.1.5.5.7.3.3,1.3.6.1.5.5.7.3.1",
            "2.5.29.19={text}false"
        )
    }
    
    try {
        $cert = New-SelfSignedCertificate @certParams
        Write-CertLog "Certificate created successfully" "INFO"
        Write-CertLog "Thumbprint: $($cert.Thumbprint)" "INFO"
        Write-CertLog "Subject: $($cert.Subject)" "INFO"
        Write-CertLog "Valid until: $($cert.NotAfter)" "INFO"
        
        # Export certificate if output path specified
        if ($OutputPath) {
            if (-not $Password) {
                $Password = Read-Host "Enter password to protect the certificate file" -AsSecureString
                $Password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
            }
            
            $exportParams = @{
                FilePath = $OutputPath
                Cert = $cert
                Password = $Password
                Force = $true
            }
            
            if ($ExportPrivateKey) {
                Export-PfxCertificate @exportParams | Out-Null
                Write-CertLog "Certificate with private key exported to: $OutputPath" "INFO"
            } else {
                Export-Certificate -Cert $cert -FilePath $OutputPath -Force | Out-Null
                Write-CertLog "Certificate (public key only) exported to: $OutputPath" "INFO"
            }
        }
        
        return $cert
    }
    catch {
        Write-CertLog "Failed to create certificate: $($_.Exception.Message)" "ERROR"
        throw
    }
}

function Get-CertificateStore {
    param([string]$StorePath)
    
    try {
        if (-not (Test-Path $StorePath)) {
            Write-CertLog "Certificate store not found: $StorePath" "WARN"
            return $null
        }
        
        return Get-ChildItem -Path $StorePath
    }
    catch {
        Write-CertLog "Failed to access certificate store: $StorePath" "ERROR"
        return $null
    }
}

function Import-CertificateToStore {
    param([string]$CertificatePath, [string]$StorePath, [string]$Password)
    
    Write-CertLog "Importing certificate from: $CertificatePath" "INFO"
    
    if (-not (Test-Path $CertificatePath)) {
        throw "Certificate file not found: $CertificatePath"
    }
    
    try {
        if ($CertificatePath -match "\.pfx$") {
            # Import PFX with private key
            if (-not $Password) {
                $Password = Read-Host "Enter password for certificate file" -AsSecureString
                $Password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
            }
            
            $importParams = @{
                FilePath = $CertificatePath
                CertStoreLocation = $StorePath
                Password = $Password
            }
            
            $cert = Import-PfxCertificate @importParams
            Write-CertLog "PFX certificate imported successfully" "INFO"
            Write-CertLog "Thumbprint: $($cert.Thumbprint)" "INFO"
            
            return $cert
        }
        else {
            # Import CER (public key only)
            $importParams = @{
                FilePath = $CertificatePath
                CertStoreLocation = $StorePath
            }
            
            $cert = Import-Certificate @importParams
            Write-CertLog "CER certificate imported successfully" "INFO"
            Write-CertLog "Thumbprint: $($cert.Thumbprint)" "INFO"
            
            return $cert
        }
    }
    catch {
        Write-CertLog "Failed to import certificate: $($_.Exception.Message)" "ERROR"
        throw
    }
}

function Export-CertificateFromStore {
    param([string]$Thumbprint, [string]$OutputPath, [string]$Password, [string]$StorePath, [switch]$ExportPrivateKey)
    
    Write-CertLog "Exporting certificate with thumbprint: $Thumbprint" "INFO"
    
    # Find certificate in store
    $cert = Get-ChildItem -Path $StorePath | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
    
    if (-not $cert) {
        throw "Certificate with thumbprint $Thumbprint not found in store: $StorePath"
    }
    
    Write-CertLog "Certificate found: $($cert.Subject)" "INFO"
    Write-CertLog "Valid until: $($cert.NotAfter)" "INFO"
    
    # Check if certificate is suitable for code signing
    $eku = $cert.EnhancedKeyUsageList | Where-Object { $_.FriendlyName -eq "Code Signing" }
    if (-not $eku) {
        Write-CertLog "Warning: Certificate may not be suitable for code signing (missing Code Signing EKU)" "WARN"
    }
    
    if ($ExportPrivateKey) {
        # Export as PFX
        if (-not $Password) {
            $Password = Read-Host "Enter password to protect exported certificate" -AsSecureString
            $Password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
        }
        
        $exportParams = @{
            FilePath = $OutputPath
            Cert = $cert
            Password = $Password
            Force = $true
        }
        
        Export-PfxCertificate @exportParams | Out-Null
        Write-CertLog "Certificate exported to PFX: $OutputPath" "INFO"
    }
    else {
        # Export as CER (public key only)
        Export-Certificate -Cert $cert -FilePath $OutputPath -Force | Out-Null
        Write-CertLog "Certificate exported to CER: $OutputPath" "INFO"
    }
}

function Test-CertificateValidity {
    param([string]$Thumbprint, [string]$StorePath)
    
    Write-CertLog "Testing certificate validity for thumbprint: $Thumbprint" "INFO"
    
    $cert = Get-ChildItem -Path $StorePath | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
    
    if (-not $cert) {
        Write-CertLog "Certificate with thumbprint $Thumbprint not found" "ERROR"
        return $false
    }
    
    $now = Get-Date
    $isExpired = $cert.NotAfter -lt $now
    $isNotYetValid = $cert.NotBefore -gt $now
    
    Write-CertLog "Subject: $($cert.Subject)" "INFO"
    Write-CertLog "Thumbprint: $($cert.Thumbprint)" "INFO"
    Write-CertLog "Valid from: $($cert.NotBefore)" "INFO"
    Write-CertLog "Valid until: $($cert.NotAfter)" "INFO"
    Write-CertLog "Is expired: $isExpired" $(if ($isExpired) { "ERROR" } else { "INFO" })
    Write-CertLog "Is not yet valid: $isNotYetValid" $(if ($isNotYetValid) { "ERROR" } else { "INFO" })
    
    # Check enhanced key usage
    $hasCodeSigning = $cert.EnhancedKeyUsageList | Where-Object { $_.FriendlyName -eq "Code Signing" }
    Write-CertLog "Has Code Signing EKU: $($hasCodeSigning -ne $null)" $(if (-not $hasCodeSigning) { "WARN" } else { "INFO" })
    
    # Check if certificate can be used for signing
    $isValidForSigning = -not $isExpired -and -not $isNotYetValid -and $hasCodeSigning
    Write-CertLog "Certificate is valid for code signing: $isValidForSigning" $(if ($isValidForSigning) { "INFO" } else { "WARN" })
    
    return $isValidForSigning
}

function Show-CertificateInfo {
    param([string]$Thumbprint, [string]$StorePath)
    
    Write-CertLog "Certificate Information" "INFO"
    Write-CertLog "================================" "INFO"
    
    $cert = if ($Thumbprint) {
        Get-ChildItem -Path $StorePath | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
    } else {
        Get-ChildItem -Path $StorePath | Select-Object -First 1
    }
    
    if (-not $cert) {
        Write-CertLog "No certificate found" "ERROR"
        return
    }
    
    Write-CertLog "Subject: $($cert.Subject)" "INFO"
    Write-CertLog "Thumbprint: $($cert.Thumbprint)" "INFO"
    Write-CertLog "Serial Number: $($cert.SerialNumber)" "INFO"
    Write-CertLog "Issuer: $($cert.Issuer)" "INFO"
    Write-CertLog "Valid From: $($cert.NotBefore)" "INFO"
    Write-CertLog "Valid Until: $($cert.NotAfter)" "INFO"
    Write-CertLog "Has Private Key: $($cert.HasPrivateKey)" "INFO"
    
    Write-CertLog "Enhanced Key Usage:" "INFO"
    foreach ($eku in $cert.EnhancedKeyUsageList) {
        Write-CertLog "  - $($eku.FriendlyName) ($($eku.ObjectId))" "DEBUG"
    }
    
    Write-CertLog "Key Usage:" "INFO"
    $keyUsage = $cert.KeyUsages
    if ($keyUsage) {
        Write-CertLog "  - $($keyUsage -join ', ')" "DEBUG"
    }
}

function Remove-CertificateFromStore {
    param([string]$Thumbprint, [string]$StorePath)
    
    Write-CertLog "Removing certificate with thumbprint: $Thumbprint" "INFO"
    
    $cert = Get-ChildItem -Path $StorePath | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
    
    if (-not $cert) {
        Write-CertLog "Certificate with thumbprint $Thumbprint not found in store" "ERROR"
        return $false
    }
    
    try {
        Remove-Item -Path $cert.PSPath -Force
        Write-CertLog "Certificate removed successfully" "INFO"
        return $true
    }
    catch {
        Write-CertLog "Failed to remove certificate: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Main execution
try {
    switch ($Action) {
        "Create" {
            Write-CertLog "Creating self-signed code signing certificate" "INFO"
            $cert = New-SelfSignedCertificate -Subject $Subject -ValidityDays $ValidityDays -OutputPath $OutputPath -Password $Password
            
            if ($cert) {
                Write-CertLog "Certificate created successfully" "INFO"
                Write-CertLog "Thumbprint: $($cert.Thumbprint)" "INFO"
                Write-CertLog "Use this thumbprint for signing operations" "INFO"
            }
        }
        
        "Import" {
            if (-not $CertificatePath) {
                throw "CertificatePath is required for Import action"
            }
            $cert = Import-CertificateToStore -CertificatePath $CertificatePath -StorePath $CertificateStore -Password $Password
        }
        
        "Export" {
            if (-not $CertificateThumbprint) {
                throw "CertificateThumbprint is required for Export action"
            }
            if (-not $OutputPath) {
                throw "OutputPath is required for Export action"
            }
            Export-CertificateFromStore -Thumbprint $CertificateThumbprint -OutputPath $OutputPath -Password $Password -StorePath $CertificateStore -ExportPrivateKey:$ExportPrivateKey
        }
        
        "List" {
            Write-CertLog "Listing certificates in store: $CertificateStore" "INFO"
            $certs = Get-CertificateStore -StorePath $CertificateStore
            
            if ($certs) {
                Write-CertLog "Found $($certs.Count) certificate(s)" "INFO"
                foreach ($cert in $certs) {
                    Write-CertLog "--------------------------------" "INFO"
                    Write-CertLog "Subject: $($cert.Subject)" "INFO"
                    Write-CertLog "Thumbprint: $($cert.Thumbprint)" "INFO"
                    Write-CertLog "Valid Until: $($cert.NotAfter)" "INFO"
                    Write-CertLog "Has Private Key: $($cert.HasPrivateKey)" "INFO"
                }
            } else {
                Write-CertLog "No certificates found in store" "INFO"
            }
        }
        
        "Verify" {
            if (-not $CertificateThumbprint) {
                throw "CertificateThumbprint is required for Verify action"
            }
            $isValid = Test-CertificateValidity -Thumbprint $CertificateThumbprint -StorePath $CertificateStore
            
            if (-not $isValid) {
                Write-CertLog "Certificate verification failed" "ERROR"
                exit 1
            }
            Write-CertLog "Certificate verification passed" "INFO"
        }
        
        "Info" {
            Show-CertificateInfo -Thumbprint $CertificateThumbprint -StorePath $CertificateStore
        }
        
        "Remove" {
            if (-not $CertificateThumbprint) {
                throw "CertificateThumbprint is required for Remove action"
            }
            $removed = Remove-CertificateFromStore -Thumbprint $CertificateThumbprint -StorePath $CertificateStore
            
            if ($removed) {
                Write-CertLog "Certificate removed successfully" "INFO"
            } else {
                Write-CertLog "Failed to remove certificate" "ERROR"
                exit 1
            }
        }
        
        default {
            throw "Unknown action: $Action"
        }
    }
    
    Write-CertLog "Certificate management operation completed successfully" "INFO"
    exit 0
}
catch {
    Write-CertLog "Certificate management failed: $($_.Exception.Message)" "ERROR"
    Write-CertLog $_.ScriptStackTrace "DEBUG"
    exit 1
}