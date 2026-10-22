// Mobile sidebar toggle
document.addEventListener('click', function (e) {
  var t = e.target.closest('[data-toggle-sidebar]');
  var sb = document.getElementById('sidebar');
  if (t && sb) { sb.classList.toggle('open'); return; }
  if (sb && sb.classList.contains('open') && !e.target.closest('#sidebar')) sb.classList.remove('open');
});

// Show / hide password
document.querySelectorAll('[data-toggle-password]').forEach(function (btn) {
  btn.addEventListener('click', function () {
    var input = document.querySelector(btn.getAttribute('data-toggle-password'));
    if (!input) return;
    var show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    btn.querySelector('i').className = show ? 'bi bi-eye-slash' : 'bi bi-eye';
  });
});

// Auto-dismiss success alerts
setTimeout(function () {
  document.querySelectorAll('.alert-success.auto-dismiss').forEach(function (a) { a.classList.remove('show'); });
}, 5000);

// Request-room form: only show rooms that belong to the selected hostel
(function () {
  var hostel = document.getElementById('PreferredHostelId'), room = document.getElementById('PreferredRoomId');
  if (!hostel || !room) return;
  function filter() {
    var h = hostel.value;
    Array.prototype.forEach.call(room.options, function (o) {
      var show = !o.value || !h || o.getAttribute('data-hostel') === h;
      o.hidden = !show; o.disabled = !show;
    });
    if (room.selectedOptions.length && room.selectedOptions[0].disabled) room.value = '';
  }
  hostel.addEventListener('change', filter); filter();
})();
