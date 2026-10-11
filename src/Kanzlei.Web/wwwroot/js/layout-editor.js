(function () {
  var hexPattern = /^#[0-9a-fA-F]{6}$/;
  var preview = document.getElementById('themePreview');
  var hint = document.getElementById('contrastHint');
  var form = document.getElementById('layoutForm');
  if (!preview || !form || !hint) return;

  function lum(hex) {
    var ch = [1, 3, 5].map(function (i) {
      var c = parseInt(hex.substr(i, 2), 16) / 255;
      return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
    });
    return 0.2126 * ch[0] + 0.7152 * ch[1] + 0.0722 * ch[2];
  }
  function ratio(a, b) {
    var x = lum(a), y = lum(b);
    return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
  }
  function update() {
    var v = {};
    form.querySelectorAll('input[type=text][data-css-var]').forEach(function (input) {
      if (hexPattern.test(input.value)) {
        preview.style.setProperty(input.dataset.cssVar, input.value);
        v[input.dataset.token] = input.value;
      }
    });
    var onPrimary = v.primary && ratio(v.primary, '#ffffff') >= ratio(v.primary, '#000000') ? '#ffffff' : '#000000';
    preview.style.setProperty('--app-primary-contrast', onPrimary);
    var onCal = v.calendar && ratio(v.calendar, '#ffffff') >= ratio(v.calendar, '#000000') ? '#ffffff' : '#000000';
    preview.style.setProperty('--app-calendar-contrast', onCal);
    if (v.text && v.surface && v.background) {
      var a = ratio(v.text, v.surface), b = ratio(v.text, v.background);
      var min = Math.min(a, b);
      hint.textContent = min < 4.5
        ? 'Hinweis: Der Kontrast zwischen Haupttext und Hintergrund/Fläche beträgt nur ' + min.toFixed(1) + ':1 (empfohlen: mindestens 4,5:1).'
        : 'Kontrast Haupttext zu Hintergrund/Fläche: ' + min.toFixed(1) + ':1 – ausreichend.';
    }
  }
  form.querySelectorAll('input[data-token]').forEach(function (input) {
    input.addEventListener('input', function () {
      var key = input.dataset.token;
      if (input.type === 'color') {
        form.querySelector('input[type=text][data-token="' + key + '"]').value = input.value;
      } else if (hexPattern.test(input.value)) {
        form.querySelector('input[type=color][data-token="' + key + '"]').value = input.value;
      }
      update();
    });
  });
  update();
})();
