$ErrorActionPreference='Stop'
$ga = Split-Path $PSScriptRoot -Parent
$src = Join-Path $ga 'src'
$utf8 = New-Object System.Text.UTF8Encoding $false

function Patch([string]$rel, [scriptblock]$fn) {
  $p = Join-Path $src $rel
  $t = [IO.File]::ReadAllText($p)
  $n = & $fn $t
  if ($n -ne $t) { [IO.File]::WriteAllText($p, $n, $utf8); Write-Host "patched $rel" } else { Write-Host "unchanged $rel" }
}

Patch 'GaMode.cs' {
  param($t)
  if ($t -notmatch '_nextHudKickAt') {
    if ($t -match 'internal static bool Active;') {
      $t = $t.Replace('internal static bool Active;', "internal static bool Active;`r`n        private static float _nextHudKickAt;`r`n        private static float _nextHoldFlightAt;")
    } elseif ($t -match 'internal static bool Dead;') {
      $t = $t.Replace('internal static bool Dead;', "internal static bool Dead;`r`n        private static float _nextHudKickAt;`r`n        private static float _nextHoldFlightAt;")
    }
  }
  $old = "            HoldFlightControls();`r`n            FreezeAircraft();`r`n            HideAircraftHud();`r`n            KickCockpitCam();"
  $new = @"
            float nowKick = Time.unscaledTime;
            if (nowKick >= _nextHoldFlightAt)
            {
                _nextHoldFlightAt = nowKick + 0.25f;
                HoldFlightControls();
            }
            FreezeAircraft();
            if (nowKick >= _nextHudKickAt)
            {
                _nextHudKickAt = nowKick + 0.5f;
                HideAircraftHud();
                KickCockpitCam();
            }
"@
  if ($t.Contains($old)) { $t = $t.Replace($old, $new) }
  else {
    $old2 = $old.Replace("`r`n","`n")
    $new2 = $new.Replace("`r`n","`n")
    if ($t.Contains($old2)) { $t = $t.Replace($old2, $new2) }
  }
  return $t
}

Patch 'GaCombat.cs' {
  param($t)
  if ($t -notmatch '_rayBuf') {
    $t = $t.Replace(
      'private static readonly List<Bolt> Bolts = new List<Bolt>(32);',
      "private static readonly List<Bolt> Bolts = new List<Bolt>(32);`r`n        private static readonly RaycastHit[] _rayBuf = new RaycastHit[48];`r`n        private static int _fxShotN;")
  }
  $t = $t.Replace('Physics.RaycastAll(origin, dir, range,', 'Physics.RaycastNonAlloc(origin, dir, _rayBuf, range, /*was All*/')
  # That breaks syntax - do proper replace of whole WeaponRay body via markers
  return $t
}

Write-Host 'partial - combat needs careful edit'
