// Forms marked data-confirm ask first. (An inline onsubmit would be blocked by the Content-Security-Policy.)
document.addEventListener("submit", (e) => {
  const message = e.target.dataset?.confirm;
  if (message && !window.confirm(message)) e.preventDefault();
});
