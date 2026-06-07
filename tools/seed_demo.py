"""Seed de dados de demonstração para screenshots do README (uso local/descartável)."""
import json, ssl, urllib.request, urllib.error
from http.cookiejar import CookieJar

BASE = "https://localhost:5543/api"
CTX = ssl._create_unverified_context()

def client():
    return urllib.request.build_opener(
        urllib.request.HTTPSHandler(context=CTX),
        urllib.request.HTTPCookieProcessor(CookieJar()))

def call(op, method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    try:
        with op.open(req, timeout=30) as r:
            raw = r.read().decode() or "{}"
            return r.status, (json.loads(raw) if raw.strip().startswith(("{", "[")) else raw)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()

# --- admin ---
admin = client()
st, _ = call(admin, "POST", "/auth/login", {"email": "admin@deskcore.local", "password": "ChangeMe@123456"})
print("admin login:", st)
assert st == 200

# --- categorias ---
st, cats = call(admin, "GET", "/categories")
catmap = {c["name"]: c["id"] for c in (cats if isinstance(cats, list) else cats.get("items", []))}
print("categorias:", catmap)

# --- usuários ---
def ensure_user(email, name, role, pwd):
    st, r = call(admin, "POST", "/users", {"email": email, "fullName": name, "password": pwd, "role": role})
    if st in (200, 201): print(f"user {email}: criado"); return r["id"]
    print(f"user {email}: {st} {r}")
    st, lst = call(admin, "GET", "/users?pageSize=100")
    items = lst.get("items", lst) if isinstance(lst, dict) else lst
    return next(u["id"] for u in items if u["email"].lower() == email.lower())

agent_id = ensure_user("ana.suporte@deskcore.local", "Ana Suporte", "Agent", "Suporte@Desk2026")
client_id = ensure_user("joao.pereira@empresa.com", "João Pereira", "User", "Cliente@Desk2026")

# --- cliente cria tickets ---
cli = client()
st, _ = call(cli, "POST", "/auth/login", {"email": "joao.pereira@empresa.com", "password": "Cliente@Desk2026"})
print("cliente login:", st)

TICKETS = [
    ("Impressora do financeiro não imprime", "A impressora HP do setor financeiro parou de imprimir desde hoje cedo. Aparece erro de comunicação.", 2, "Impressora"),
    ("Não consigo acessar o e-mail corporativo", "Ao tentar logar no Outlook recebo 'senha incorreta', mas a senha está certa. Preciso com urgência.", 3, "Acesso"),
    ("Solicitação de instalação do Office", "Gostaria da instalação do pacote Office no meu notebook novo.", 0, "Software"),
    ("Internet lenta no 3º andar", "A conexão no 3º andar está muito lenta desde ontem à tarde, afetando toda a equipe.", 1, "Rede"),
    ("Notebook não liga após atualização", "Depois da atualização do Windows o notebook não inicia mais, fica em tela preta.", 2, "Hardware"),
    ("Erro ao emitir nota fiscal no sistema", "O sistema interno retorna erro 500 ao tentar emitir NF-e. Bloqueando o faturamento.", 3, "Sistema Interno"),
    ("Acesso à pasta compartilhada do RH", "Preciso de permissão de leitura na pasta compartilhada do RH para o novo processo.", 1, "Acesso"),
    ("Monitor com falha de imagem", "O monitor secundário fica piscando e às vezes apaga. Já troquei o cabo.", 0, "Hardware"),
]
ids = []
for title, desc, prio, cat in TICKETS:
    st, r = call(cli, "POST", "/tickets", {"title": title, "description": desc, "priority": prio, "categoryId": catmap.get(cat, list(catmap.values())[0])})
    if st in (200, 201): ids.append(r["id"]); print(f"ticket {r['number']}: {title}")
    else: print("ticket FAIL", st, r)

def assign(tid, uid): call(admin, "POST", f"/tickets/{tid}/assign", {"assignedToUserId": uid})
def status(tid, s): print("  status", tid, "->", s, call(admin, "POST", f"/tickets/{tid}/status", {"status": s})[0])
def comment(tid, msg, internal=False): call(admin, "POST", f"/tickets/{tid}/comments", {"message": msg, "isInternal": internal})

# --- fluxo: espalhar status/atribuições/comentários ---
# t0: atribui Ana -> InProgress -> comentário público -> WaitingUser
assign(ids[0], agent_id); comment(ids[0], "Olá! Estamos verificando a impressora. Pode confirmar se o cabo de rede está conectado?"); status(ids[0], 3)
# t1: atribui Ana -> comentário interno -> Resolved
assign(ids[1], agent_id); comment(ids[1], "Resetei a senha no AD e orientei o usuário. Aguardando confirmação.", True); status(ids[1], 4)
# t2: atribui admin -> Resolved -> Closed
assign(ids[2], None) if False else assign(ids[2], agent_id); status(ids[2], 4); status(ids[2], 5)
# t3: atribui Ana -> InProgress (fica em atendimento)
assign(ids[3], agent_id)
# t4: fica WaitingAgent (novo)
# t5: atribui Ana -> comentário público -> Resolved
assign(ids[5], agent_id); comment(ids[5], "Identificamos um bug no módulo de NF-e. Correção aplicada, favor testar."); status(ids[5], 4)
# t6: fica WaitingAgent (novo)
# t7: atribui Ana -> Resolved -> Closed
assign(ids[7], agent_id); status(ids[7], 4); status(ids[7], 5)

print("SEED OK — tickets:", len(ids))
