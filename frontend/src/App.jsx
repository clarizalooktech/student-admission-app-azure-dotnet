// ── Step definitions ─────────────────────────────────────────────────────────
const STEPS = ['Personal', 'Academic', 'Documents', 'Review'];

// ── Main App ─────────────────────────────────────────────────────────────────
function App() {
  const [step, setStep] = React.useState(0);
  const [apiBase, setApiBase] = React.useState(null);
  const [configError, setConfigError] = React.useState(false);

  // Load backend URL from config.json at runtime — no hardcoding needed
  React.useEffect(() => {
    fetch('./config.json')
      .then(r => r.json())
      .then(cfg => setApiBase(cfg.apiBase))
      .catch(() => {
        // Fallback for local dev — just run the backend on 5000
        setApiBase('http://localhost:5000');
        setConfigError(true);
      });
  }, []);

  if (!apiBase) return (
    <div style={{ textAlign: 'center', padding: '3rem', color: '#666' }}>
      Loading configuration...
    </div>
  );
  const [form, setForm] = React.useState({
    firstName: '', lastName: '', email: '', phone: '', country: '',
    qualification: 'bachelors', gpa: '', field: '', institution: '', gradYear: '',
    programme: 'master-cs', startDate: 'feb-2026', statement: '',
    hasTranscript: false, hasPassport: false, hasReferences: false,
  });
  const [agentState, setAgentState] = React.useState(null);
  const [submitting, setSubmitting] = React.useState(false);

  const update = (field, value) => setForm(f => ({ ...f, [field]: value }));

  const handleSubmit = async () => {
    setSubmitting(true);
    setAgentState({ status: 'running', steps: [], decision: null });
    try {
      const res = await fetch(`${apiBase}/api/admission/evaluate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(form),
      });
      const data = await res.json();
      setAgentState(data);
    } catch (err) {
      setAgentState({ status: 'error', steps: [], decision: { outcome: 'error', summary: 'Could not reach the backend. Check that the API is running.' } });
    }
    setSubmitting(false);
  };

  return (
    <div>
      <div className="app-header">
        <h1>Student admission application</h1>
        <p>Powered by an AI agent on Azure Container Apps</p>
      </div>

      <StepBar current={step} />

      <div className="card">
        {step === 0 && <StepPersonal form={form} update={update} />}
        {step === 1 && <StepAcademic form={form} update={update} />}
        {step === 2 && <StepDocuments form={form} update={update} />}
        {step === 3 && <StepReview form={form} agentState={agentState} submitting={submitting} onSubmit={handleSubmit} />}

        {step < 3 && (
          <div className="actions">
            <button className="btn-back" onClick={() => setStep(s => s - 1)} disabled={step === 0}>
              ← Back
            </button>
            <button className="btn-next" onClick={() => setStep(s => s + 1)}>
              Continue →
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

// ── Step: Progress bar ────────────────────────────────────────────────────────
function StepBar({ current }) {
  return (
    <div className="steps">
      {STEPS.map((label, i) => (
        <React.Fragment key={i}>
          <div className="step-item">
            <div className={`step-circle ${i < current ? 'done' : i === current ? 'active' : ''}`}>
              {i < current ? '✓' : i + 1}
            </div>
            <div className={`step-label ${i === current ? 'active' : ''}`}>{label}</div>
          </div>
          {i < STEPS.length - 1 && (
            <div className={`step-connector ${i < current ? 'done' : ''}`} />
          )}
        </React.Fragment>
      ))}
    </div>
  );
}

// ── Step 1: Personal info ─────────────────────────────────────────────────────
function StepPersonal({ form, update }) {
  return (
    <>
      <div className="card-title">Personal information</div>
      <div className="card-sub">Basic details about you as the applicant.</div>
      <div className="form-row">
        <Field label="First name" value={form.firstName} onChange={v => update('firstName', v)} placeholder="Ada" />
        <Field label="Last name"  value={form.lastName}  onChange={v => update('lastName', v)}  placeholder="Lovelace" />
      </div>
      <Field label="Email address" value={form.email} onChange={v => update('email', v)} placeholder="ada@example.com" type="email" />
      <div className="form-row">
        <Field label="Phone number" value={form.phone} onChange={v => update('phone', v)} placeholder="+61 4xx xxx xxx" />
        <SelectField label="Country of citizenship" value={form.country} onChange={v => update('country', v)}
          options={[['', 'Select country'], ['au', 'Australia'], ['uk', 'United Kingdom'], ['us', 'United States'], ['in', 'India'], ['cn', 'China'], ['other', 'Other']]} />
      </div>
    </>
  );
}

// ── Step 2: Academic record ───────────────────────────────────────────────────
function StepAcademic({ form, update }) {
  return (
    <>
      <div className="card-title">Academic background</div>
      <div className="card-sub">The AI agent uses this to check eligibility requirements.</div>
      <div className="form-row">
        <SelectField label="Highest qualification" value={form.qualification} onChange={v => update('qualification', v)}
          options={[['bachelors', "Bachelor's degree"], ['masters', "Master's degree"], ['diploma', 'Diploma'], ['other', 'Other']]} />
        <Field label="GPA / grade average" value={form.gpa} onChange={v => update('gpa', v)} placeholder="e.g. 3.7 / 4.0" />
      </div>
      <Field label="Field of study" value={form.field} onChange={v => update('field', v)} placeholder="e.g. Computer Science" />
      <div className="form-row">
        <Field label="Institution" value={form.institution} onChange={v => update('institution', v)} placeholder="University name" />
        <Field label="Graduation year" value={form.gradYear} onChange={v => update('gradYear', v)} placeholder="YYYY" />
      </div>
    </>
  );
}

// ── Step 3: Documents & programme ────────────────────────────────────────────
function StepDocuments({ form, update }) {
  return (
    <>
      <div className="card-title">Programme & documents</div>
      <div className="card-sub">Choose your programme and confirm your supporting documents.</div>
      <div className="form-row">
        <SelectField label="Programme" value={form.programme} onChange={v => update('programme', v)}
          options={[['master-cs', 'Master of Computer Science'], ['master-ba', 'Master of Business Admin'], ['master-eng', 'Master of Engineering'], ['master-data', 'Master of Data Science']]} />
        <SelectField label="Intake" value={form.startDate} onChange={v => update('startDate', v)}
          options={[['feb-2026', 'February 2026'], ['jul-2026', 'July 2026'], ['feb-2027', 'February 2027']]} />
      </div>
      <div className="form-group">
        <label>Personal statement</label>
        <textarea value={form.statement} onChange={e => update('statement', e.target.value)}
          placeholder="Briefly describe your motivation and goals (2–3 sentences)..." />
      </div>
      <div className="form-group">
        <label>Supporting documents (confirm available)</label>
        <CheckItem label="Academic transcript" checked={form.hasTranscript} onChange={v => update('hasTranscript', v)} />
        <CheckItem label="Passport / ID" checked={form.hasPassport} onChange={v => update('hasPassport', v)} />
        <CheckItem label="Reference letters (2)" checked={form.hasReferences} onChange={v => update('hasReferences', v)} />
      </div>
    </>
  );
}

// ── Step 4: Review + AI result ───────────────────────────────────────────────
function StepReview({ form, agentState, submitting, onSubmit }) {
  return (
    <>
      <div className="card-title">Review & submit</div>
      <div className="card-sub">The AI agent will evaluate your application in real time.</div>

      <ReviewRow label="Name"         value={`${form.firstName} ${form.lastName}`} />
      <ReviewRow label="Email"        value={form.email} />
      <ReviewRow label="Qualification" value={form.qualification} />
      <ReviewRow label="GPA"          value={form.gpa} />
      <ReviewRow label="Programme"    value={form.programme} />
      <ReviewRow label="Intake"       value={form.startDate} />

      {!agentState && (
        <div className="actions">
          <span style={{ fontSize: 13, color: '#999' }}>Ready to submit</span>
          <button className="btn-next" onClick={onSubmit} disabled={submitting}>
            {submitting ? 'Submitting...' : 'Submit & evaluate →'}
          </button>
        </div>
      )}

      {agentState && <AgentPanel state={agentState} />}
    </>
  );
}

// ── AI Agent Panel ────────────────────────────────────────────────────────────
function AgentPanel({ state }) {
  const icons = { done: '✓', active: '▶', pending: '○', error: '✕' };

  return (
    <>
      <div className="agent-panel">
        <div className="agent-panel-header">
          <div className={`agent-dot ${state.status === 'running' ? 'thinking' : ''}`} />
          <div className="agent-panel-title">
            {state.status === 'running' ? 'AI agent is evaluating...' : 'Agent evaluation complete'}
          </div>
        </div>
        {(state.steps || []).map((s, i) => (
          <div key={i} className={`agent-step ${s.status}`}>
            <span className="agent-step-icon">{icons[s.status] || '○'}</span>
            <span>{s.label}</span>
          </div>
        ))}
      </div>

      {state.decision && <DecisionCard decision={state.decision} />}

      <div className="obs-bar">
        <span className="obs-pill live">App Insights ✓</span>
        <span className="obs-pill live">Prometheus metrics ✓</span>
        <span className="obs-pill">Grafana dashboard</span>
        <span className="obs-pill">Trace ID: {state.traceId || 'n/a'}</span>
      </div>
    </>
  );
}

function DecisionCard({ decision }) {
  const cls = decision.outcome === 'approved' ? 'approved'
            : decision.outcome === 'review'   ? 'review'
            : 'declined';
  const title = decision.outcome === 'approved' ? 'Conditionally approved'
              : decision.outcome === 'review'   ? 'Referred for manual review'
              : decision.outcome === 'error'    ? 'Connection error'
              : 'Not eligible at this time';
  return (
    <div className={`decision-card ${cls}`}>
      <div className="decision-title">{title}</div>
      <div className="decision-body">{decision.summary}</div>
    </div>
  );
}

// ── Small helpers ─────────────────────────────────────────────────────────────
function Field({ label, value, onChange, placeholder, type = 'text' }) {
  return (
    <div className="form-group">
      <label>{label}</label>
      <input type={type} value={value} placeholder={placeholder} onChange={e => onChange(e.target.value)} />
    </div>
  );
}

function SelectField({ label, value, onChange, options }) {
  return (
    <div className="form-group">
      <label>{label}</label>
      <select value={value} onChange={e => onChange(e.target.value)}>
        {options.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
      </select>
    </div>
  );
}

function CheckItem({ label, checked, onChange }) {
  return (
    <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6, cursor: 'pointer', fontSize: 14 }}>
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)}
        style={{ width: 16, height: 16 }} />
      {label}
    </label>
  );
}

function ReviewRow({ label, value }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '7px 0', borderBottom: '1px solid #f5f5f5', fontSize: 14 }}>
      <span style={{ color: '#666' }}>{label}</span>
      <span style={{ fontWeight: 500 }}>{value || '—'}</span>
    </div>
  );
}

// ── Mount ─────────────────────────────────────────────────────────────────────
const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<App />);
