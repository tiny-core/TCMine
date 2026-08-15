// Rola o console para o fim.
//
// Só rola se o admin já estava perto do fim: se ele subiu para ler uma exceção,
// puxar a tela de volta a cada linha nova tornaria o log ilegível justo quando
// ele mais importa.
export function scrollToBottom(id) {
  const el = document.getElementById(id);
  if (!el) return;

  const distanciaDoFim = el.scrollHeight - el.scrollTop - el.clientHeight;
  if (distanciaDoFim > 120) return;

  el.scrollTop = el.scrollHeight;
}
