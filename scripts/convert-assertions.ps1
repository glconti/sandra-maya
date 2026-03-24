# Simple codemod to convert common xUnit Assert usages to AwesomeAssertions fluent style
# NOTE: This is a best-effort regex-based converter. Review changes manually after running.

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$testDir = Join-Path $PSScriptRoot "..\tests"
Write-Host "Scanning $testDir for .cs files..."

Get-ChildItem -Path $testDir -Recurse -Include *.cs | ForEach-Object {
    $path = $_.FullName
    $content = Get-Content -Raw -Path $path -Encoding UTF8
    $orig = $content

    # Replace Assert.Equal(expected, actual) => actual.Should().Be(expected)
    $content = [regex]::Replace($content, 'Assert\.Equal\(([^,\)]+)\s*,\s*([^\)]+)\)', '$2.Should().Be($1)')

    # Replace Assert.NotEqual(expected, actual) => actual.Should().NotBe(expected)
    $content = [regex]::Replace($content, 'Assert\.NotEqual\(([^,\)]+)\s*,\s*([^\)]+)\)', '$2.Should().NotBe($1)')

    # Replace Assert.True(condition) => condition.Should().BeTrue()
    $content = [regex]::Replace($content, 'Assert\.True\(([^\)]+)\)', '$1.Should().BeTrue()')

    # Replace Assert.False(condition) => condition.Should().BeFalse()
    $content = [regex]::Replace($content, 'Assert\.False\(([^\)]+)\)', '$1.Should().BeFalse()')

    # Replace Assert.Null(obj) => obj.Should().BeNull()
    $content = [regex]::Replace($content, 'Assert\.Null\(([^\)]+)\)', '$1.Should().BeNull()')

    # Replace Assert.NotNull(obj) => obj.Should().NotBeNull()
    $content = [regex]::Replace($content, 'Assert\.NotNull\(([^\)]+)\)', '$1.Should().NotBeNull()')

    # Replace Assert.Throws<ExceptionType>(() => action) => (() => action).Should().Throw<ExceptionType>()
    $content = [regex]::Replace($content, 'Assert\.Throws<([^>]+)>\(([^\)]+)\)', '$2.Should().Throw<$1>()')

    if ($content -ne $orig) {
        Write-Host "Updating $path"
        Set-Content -Path $path -Value $content -Encoding UTF8
    }
}

Write-Host "Conversion complete. Please review changes and run tests."