// DeskCore é dark-only nesta fase. Mantemos o atributo fixo em 'dark' e a função
// de toggle como no-op (placeholder para um futuro tema claro). O <html> já vem
// com data-bs-theme="dark" no App.razor; isto apenas garante consistência.
(function () {
    document.documentElement.setAttribute('data-bs-theme', 'dark');
})();

function deskcoreToggleTheme() {
    // Reservado para o futuro tema claro. Sem efeito enquanto dark-only.
}
