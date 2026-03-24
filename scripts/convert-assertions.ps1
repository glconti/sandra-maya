# Safer codemod: handle messages and avoid breaking complex expressions
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$testDir = Join-Path $PSScriptRoot "..\tests"
Write-Host "Scanning $testDir for .cs files..."

Get-ChildItem -Path $testDir -Recurse -Include *.cs | ForEach-Object {
    $path = $_.FullName
    $content = Get-Content -Raw -Path $path -Encoding UTF8
    $orig = $content

    # Assert.True with message
    $content = [regex]::Replace($content, 'Assert\.True\(([^,\)]+)\s*,\s*([^\)]+)\)', '$1.Should().BeTrue($2)')
    # Assert.True without message
    $content = [regex]::Replace($content, 'Assert\.True\(([^\)]+)\)', '$1.Should().BeTrue()')

    # Assert.False with message
    $content = [regex]::Replace($content, 'Assert\.False\(([^,\)]+)\s*,\s*([^\)]+)\)', '$1.Should().BeFalse($2)')
    # Assert.False without message
    $content = [regex]::Replace($content, 'Assert\.False\(([^\)]+)\)', '$1.Should().BeFalse()')

    # Assert.Null with message
    $content = [regex]::Replace($content, 'Assert\.Null\(([^,\)]+)\s*,\s*([^\)]+)\)', '$1.Should().BeNull($2)')
    # Assert.Null without message
    $content = [regex]::Replace($content, 'Assert\.Null\(([^\)]+)\)', '$1.Should().BeNull()')

    # Assert.NotNull with message
    $content = [regex]::Replace($content, 'Assert\.NotNull\(([^,\)]+)\s*,\s*([^\)]+)\)', '$1.Should().NotBeNull($2)')
    # Assert.NotNull without message
    $content = [regex]::Replace($content, 'Assert\.NotNull\(([^\)]+)\)', '$1.Should().NotBeNull()')

    # Assert.Throws<T>( () => action ) => (() => action).Should().Throw<T>()
    $content = [regex]::Replace($content, 'Assert\.Throws<([^>]+)>\(([^\)]+)\)', '($2).Should().Throw<$1>()')

    # Cautiously handle Assert.Equal when first arg is a simple token or literal (no parentheses, commas)
    $content = [regex]::Replace($content, 'Assert\.Equal\(([^,\)\(\"]+|\"[^\"]*\")\s*,\s*([^\)]+)\)', '$2.Should().Be($1)')

    if ($content -ne $orig) {
        Write-Host "Updating $path"
        Set-Content -Path $path -Value $content -Encoding UTF8
    }
}

Write-Host "Conversion complete. Please review changes and run tests." 
