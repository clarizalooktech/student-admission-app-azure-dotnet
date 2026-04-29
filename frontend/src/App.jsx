function App() {
  const [step, setStep] = React.useState(0);
  const [apiBase, setApiBase] = React.useState(null);
  const [configError, setConfigError] = React.useState(false);
  const [form, setForm] = React.useState({
    firstName: '', lastName: '', email: '', phone: '', country: '',
    qualification: 'bachelors', gpa: '', field: '', institution: '', gradYear: '',
    programme: 'master-cs', startDate: 'feb-2026', statement: '',
    hasTranscript: false, hasPassport: false, hasReferences: false,
  });
  const [agentState, setAgentState] = React.useState(null);
  const [submitting, setSubmitting] = React.useState(false);

  React.useEffect(() => {
    fetch('./config.json')
      .then(r => r.json())
      .then(cfg => setApiBase(cfg.apiBase))
      .catch(() => {
        setApiBase('http://localhost:5000');
        setConfigError(true);
      });
  }, []);

  console.log('apiBase:', apiBase);
  console.log('step:', step);

  if (!apiBase) return (
    <div style={{ textAlign: 'center', padding: '3rem', color: '#666' }}>
      Loading configuration...
    </div>
  );

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