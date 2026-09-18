// Gọi endpoint kiểm tra sức khoẻ để hiển thị trạng thái máy chủ trên thanh trên.
(async function checkHealth() {
  const dot = document.getElementById('health-dot');
  const text = document.getElementById('health-text');

  try {
    const res = await fetch('/api/health', { headers: { Accept: 'application/json' } });
    if (!res.ok) throw new Error('HTTP ' + res.status);

    const data = await res.json();
    dot.className = 'dot dot-ok';
    text.textContent = `Máy chủ hoạt động · v${data.version}`;
  } catch (err) {
    dot.className = 'dot dot-bad';
    text.textContent = 'Không kết nối được máy chủ';
    console.error('Health check thất bại:', err);
  }
})();
