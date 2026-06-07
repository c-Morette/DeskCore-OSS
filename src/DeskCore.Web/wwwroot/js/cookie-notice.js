// Aviso informativo de cookies (LGPD): só cookies essenciais são usados, então não há
// opt-out — apenas transparência. A dispensa é lembrada em localStorage (não é cookie).
(function () {
    var KEY = 'dc-cookie-notice';
    try { if (localStorage.getItem(KEY) === 'ok') return; } catch (e) { /* segue exibindo */ }

    function build() {
        if (document.getElementById('dc-cookie-notice')) return;

        var en = (document.documentElement.lang || '').toLowerCase().indexOf('en') === 0;

        var bar = document.createElement('div');
        bar.id = 'dc-cookie-notice';
        bar.className = 'dc-cookie-notice';

        var txt = document.createElement('span');
        txt.innerHTML = en
            ? 'We use only essential cookies for authentication and session security. ' +
              'Learn more in the <a href="/privacidade">Privacy Policy</a>.'
            : 'Usamos apenas cookies essenciais para autenticação e segurança da sessão. ' +
              'Saiba mais na <a href="/privacidade">Política de Privacidade</a>.';

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-sm btn-primary';
        btn.textContent = en ? 'Got it' : 'Entendi';
        btn.addEventListener('click', function () {
            try { localStorage.setItem(KEY, 'ok'); } catch (e) { /* ignore */ }
            bar.remove();
        });

        bar.appendChild(txt);
        bar.appendChild(btn);
        document.body.appendChild(bar);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', build);
    } else {
        build();
    }
})();
