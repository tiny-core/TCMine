// Submete um <form> pelo id.
// Existe porque sair precisa ser POST (apagar o cookie de sessão exige um
// HttpContext vivo, e um logout por GET seria acionável por terceiros), mas o
// clique nasce num componente interativo, que não posta sozinho.
export function submitForm(id) {
  document.getElementById(id)?.submit();
}
