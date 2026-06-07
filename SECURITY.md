# Política de Segurança / Security Policy

## Reportar uma vulnerabilidade (PT-BR)

Agradecemos a divulgação responsável. **Não** abra uma issue pública para
vulnerabilidades de segurança.

- Envie um e-mail para **contato@kryden.com.br** com os detalhes
  (passos para reproduzir, impacto e, se possível, uma prova de conceito).
- Você receberá uma confirmação em até **72 horas**.
- Pedimos um prazo razoável para correção antes de qualquer divulgação pública.

## Reporting a vulnerability (English)

We appreciate responsible disclosure. Please do **not** open a public issue for
security vulnerabilities.

- Email **contato@kryden.com.br** with details (reproduction steps,
  impact and, if possible, a proof of concept).
- You will get an acknowledgement within **72 hours**.
- Please allow a reasonable time to fix the issue before any public disclosure.

## Boas práticas de implantação / Hardening checklist

Ao implantar a sua instância, garanta que:

- [ ] `.env` real **nunca** é versionado (já coberto pelo `.gitignore`).
- [ ] `SEED_ADMIN_PASSWORD` foi trocado por uma senha forte antes do primeiro start.
- [ ] `POSTGRES_PASSWORD` é forte e único.
- [ ] HTTPS está ativo com certificado válido (Let's Encrypt em produção).
- [ ] `ForwardedHeaders__KnownNetworks` aponta para a sub-rede do proxy (rate limit por IP correto).
- [ ] `Turnstile` está configurado se o auto-cadastro público estiver habilitado.
- [ ] Backups do banco e do volume de uploads estão configurados.
