const api = "/api/policies";
const state = { page: 1, pageSize: 10, totalPages: 0, items: [], search: "", status: "" };
const $ = (selector) => document.querySelector(selector);
const elements = {
  list: $("#policy-list"),
  loading: $("#loading"),
  empty: $("#empty"),
  table: $("#table-wrap"),
  pagination: $("#pagination"),
  dialog: $("#policy-dialog"),
  form: $("#policy-form"),
  error: $("#form-error"),
  save: $("#save-policy")
};

const statusLabel = { Ativa: "Ativa", Cancelada: "Cancelada", Expirada: "Expirada" };
const statusClass = { Ativa: "active", Cancelada: "cancelled", Expirada: "expired" };
const dateFormatter = new Intl.DateTimeFormat("pt-BR", { timeZone: "UTC" });
const moneyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

function formatDate(value) {
  return dateFormatter.format(new Date(`${value}T00:00:00Z`));
}

function formatDocument(value) {
  if (value.length === 11) return value.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, "$1.$2.$3-$4");
  return value.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, "$1.$2.$3/$4-$5");
}

async function request(url, options = {}) {
  const response = await fetch(url, {
    ...options,
    headers: { "Content-Type": "application/json", ...(options.headers || {}) }
  });

  if (response.status === 204) return null;
  const body = await response.json().catch(() => ({}));
  if (!response.ok) {
    const fields = body.errors ? Object.values(body.errors).flat().join(" ") : "";
    throw new Error(fields || body.detail || "Não foi possível concluir a operação.");
  }
  return body;
}

async function loadPolicies() {
  elements.loading.hidden = false;
  elements.empty.hidden = true;
  elements.table.hidden = true;
  elements.pagination.hidden = true;

  const params = new URLSearchParams({ page: state.page, pageSize: state.pageSize });
  if (state.search) params.set("search", state.search);
  if (state.status) params.set("status", state.status);

  try {
    const [result, expiring] = await Promise.all([
      request(`${api}?${params}`),
      request(`${api}/expiring?days=30`)
    ]);
    state.items = result.items;
    state.totalPages = result.totalPages;
    renderRows();
    $("#total-count").textContent = result.totalItems;
    $("#active-count").textContent = result.items.filter((x) => x.status === "Ativa").length;
    $("#expiring-count").textContent = expiring.length;
    $("#page-info").textContent = result.totalItems
      ? `Página ${result.page} de ${result.totalPages} · ${result.totalItems} registros`
      : "Nenhum registro";
    $("#previous-page").disabled = state.page <= 1;
    $("#next-page").disabled = state.page >= result.totalPages;
  } catch (error) {
    showToast(error.message);
    elements.empty.hidden = false;
  } finally {
    elements.loading.hidden = true;
  }
}

function renderRows() {
  elements.list.replaceChildren();
  elements.empty.hidden = state.items.length > 0;
  elements.table.hidden = state.items.length === 0;
  elements.pagination.hidden = state.items.length === 0;

  for (const policy of state.items) {
    const row = document.createElement("tr");
    row.innerHTML = `
      <td><strong>${policy.policyNumber}</strong><small>Criada em ${formatDate(policy.createdAt.slice(0, 10))}</small></td>
      <td><strong>${formatDocument(policy.insuredDocument)}</strong><small>CPF/CNPJ</small></td>
      <td><strong>${policy.vehiclePlate}</strong><small>Automóvel</small></td>
      <td><strong>${moneyFormatter.format(policy.monthlyPremium)}</strong><small>por mês</small></td>
      <td><strong>${formatDate(policy.coverageStartDate)} — ${formatDate(policy.coverageEndDate)}</strong><small>${daysUntil(policy.coverageEndDate)}</small></td>
      <td><span class="status-badge status-${statusClass[policy.status]}">${statusLabel[policy.status]}</span></td>
      <td><div class="actions">
        <button class="action-button edit-button" data-id="${policy.id}" aria-label="Editar ${policy.policyNumber}" title="Editar">✎</button>
        <button class="action-button delete-button" data-id="${policy.id}" aria-label="Excluir ${policy.policyNumber}" title="Excluir">⌫</button>
      </div></td>`;
    elements.list.append(row);
  }
}

function daysUntil(value) {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const target = new Date(`${value}T00:00:00`);
  const days = Math.round((target - today) / 86400000);
  if (days < 0) return `Venceu há ${Math.abs(days)} dia(s)`;
  if (days === 0) return "Vence hoje";
  return `Vence em ${days} dia(s)`;
}

function openCreateDialog() {
  elements.form.reset();
  $("#policy-id").value = "";
  $("#dialog-title").textContent = "Nova apólice";
  const today = new Date();
  const nextYear = new Date(today);
  nextYear.setFullYear(today.getFullYear() + 1);
  $("#coverage-start").value = toInputDate(today);
  $("#coverage-end").value = toInputDate(nextYear);
  $("#policy-status").value = "Ativa";
  elements.error.hidden = true;
  elements.dialog.showModal();
}

function openEditDialog(id) {
  const policy = state.items.find((item) => item.id === id);
  if (!policy) return;
  $("#policy-id").value = policy.id;
  $("#dialog-title").textContent = `Editar ${policy.policyNumber}`;
  $("#insured-document").value = formatDocument(policy.insuredDocument);
  $("#vehicle-plate").value = policy.vehiclePlate;
  $("#monthly-premium").value = policy.monthlyPremium;
  $("#coverage-start").value = policy.coverageStartDate;
  $("#coverage-end").value = policy.coverageEndDate;
  $("#policy-status").value = policy.status;
  elements.error.hidden = true;
  elements.dialog.showModal();
}

async function savePolicy(event) {
  event.preventDefault();
  elements.error.hidden = true;
  elements.save.disabled = true;
  elements.save.textContent = "Salvando…";

  const id = $("#policy-id").value;
  const payload = {
    insuredDocument: $("#insured-document").value,
    vehiclePlate: $("#vehicle-plate").value,
    monthlyPremium: Number($("#monthly-premium").value),
    coverageStartDate: $("#coverage-start").value,
    coverageEndDate: $("#coverage-end").value,
    status: $("#policy-status").value
  };

  try {
    await request(id ? `${api}/${id}` : api, {
      method: id ? "PUT" : "POST",
      body: JSON.stringify(payload)
    });
    elements.dialog.close();
    showToast(id ? "Apólice atualizada com sucesso." : "Apólice cadastrada com sucesso.");
    await loadPolicies();
  } catch (error) {
    elements.error.textContent = error.message;
    elements.error.hidden = false;
  } finally {
    elements.save.disabled = false;
    elements.save.textContent = "Salvar apólice";
  }
}

async function deletePolicy(id) {
  const policy = state.items.find((item) => item.id === id);
  if (!policy || !confirm(`Excluir a apólice ${policy.policyNumber}? Esta ação não pode ser desfeita.`)) return;
  try {
    await request(`${api}/${id}`, { method: "DELETE" });
    if (state.items.length === 1 && state.page > 1) state.page--;
    showToast("Apólice excluída com sucesso.");
    await loadPolicies();
  } catch (error) {
    showToast(error.message);
  }
}

let searchTimer;
$("#search").addEventListener("input", (event) => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    state.search = event.target.value.trim();
    state.page = 1;
    loadPolicies();
  }, 300);
});

$("#status-filter").addEventListener("change", (event) => {
  state.status = event.target.value;
  state.page = 1;
  loadPolicies();
});

elements.list.addEventListener("click", (event) => {
  const button = event.target.closest("button[data-id]");
  if (!button) return;
  if (button.classList.contains("edit-button")) openEditDialog(button.dataset.id);
  if (button.classList.contains("delete-button")) deletePolicy(button.dataset.id);
});

$("#new-policy").addEventListener("click", openCreateDialog);
$("#close-dialog").addEventListener("click", () => elements.dialog.close());
$("#cancel-dialog").addEventListener("click", () => elements.dialog.close());
elements.form.addEventListener("submit", savePolicy);
$("#previous-page").addEventListener("click", () => { state.page--; loadPolicies(); });
$("#next-page").addEventListener("click", () => { state.page++; loadPolicies(); });
elements.dialog.addEventListener("click", (event) => {
  if (event.target === elements.dialog) elements.dialog.close();
});

function toInputDate(date) {
  const offset = date.getTimezoneOffset();
  return new Date(date.getTime() - offset * 60000).toISOString().slice(0, 10);
}

function showToast(message) {
  const toast = $("#toast");
  toast.textContent = message;
  toast.classList.add("visible");
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => toast.classList.remove("visible"), 3000);
}

loadPolicies();
