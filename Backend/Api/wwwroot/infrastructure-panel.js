"use strict";
const preview = window.location.pathname === "/dev/infrastructure-health";
const endpoint = preview ? "/dev/infrastructure-health/snapshot" : "/api/v1/admin/infrastructure/health";
const labels = { database: "SQL Server", storage: "Object storage", ai: "AI integration" };
const allowed = new Set(["UP", "DOWN", "UNKNOWN"]);
const statusOf = value => allowed.has(value) ? value : "UNKNOWN";
const dateOf = value => value && Number.isFinite(Date.parse(value)) ? new Date(value).toLocaleString("en-GB") : "Not checked";
document.getElementById("preview").hidden = !preview;

async function refresh() {
  const button = document.getElementById("refresh");
  const message = document.getElementById("message");
  const components = document.getElementById("components");
  button.disabled = true;
  components.replaceChildren();
  document.getElementById("overall").textContent = "Not loaded";
  document.getElementById("checked-at").textContent = "--";
  document.getElementById("deployment").textContent = "No deployment information is loaded.";
  document.getElementById("environment").textContent = "--";
  message.textContent = "Loading health information...";
  try {
    const response = await fetch(endpoint, { credentials: "same-origin", cache: "no-store", signal: AbortSignal.timeout(5000) });
    if (response.status === 401) throw new Error("Sign in with an authorized account to view infrastructure health.");
    if (response.status === 403) throw new Error("Your account does not have permission to view infrastructure health.");
    if (!response.ok) throw new Error("Health information is unavailable. Please retry later.");
    const data = await response.json();
    if (!Array.isArray(data.components)) throw new Error("Health information could not be read.");
    document.getElementById("environment").textContent = data.environment ?? "Not configured";
    document.getElementById("overall").textContent = statusOf(data.status);
    document.getElementById("checked-at").textContent = dateOf(data.checkedAt);
    message.textContent = data.status === "UNKNOWN" ? "No verified health result is available yet." : "Latest available health snapshot.";
    for (const item of data.components) {
      const card = document.createElement("article");
      const heading = document.createElement("h3");
      heading.textContent = labels[item.name] ?? "Component";
      const badge = document.createElement("strong");
      const status = statusOf(item.status);
      badge.className = `badge ${status.toLowerCase()}`;
      badge.textContent = status;
      const checked = document.createElement("p");
      checked.textContent = `Last verified: ${dateOf(item.checkedAt)}`;
      card.append(heading, badge, checked);
      components.append(card);
    }
    document.getElementById("deployment").textContent = data.deployment
      ? `Artifact: ${data.deployment.artifactVersion ?? "--"} | Source: ${data.deployment.sourceVersion ?? "--"} | Deployed: ${dateOf(data.deployment.deployedAt)}`
      : "No verified deployment record is available yet.";
  } catch (error) {
    message.textContent = error.name === "TimeoutError" ? "The health request timed out. Please retry." : error.message;
  } finally {
    button.disabled = false;
  }
}
document.getElementById("refresh").addEventListener("click", refresh);
refresh();
