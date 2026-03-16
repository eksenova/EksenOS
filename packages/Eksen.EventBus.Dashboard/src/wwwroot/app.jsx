const { useState, useEffect, useCallback } = React;

const API_BASE = window.location.pathname.replace(/\/$/, '') + '/api';

async function apiFetch(path, options = {}) {
    const res = await fetch(`${API_BASE}${path}`, {
        headers: { 'Content-Type': 'application/json', ...options.headers },
        ...options,
    });
    if (!res.ok) throw new Error(`API error: ${res.status}`);
    return res.json();
}

const STATUS_LABELS = { 0: 'Pending', 1: 'Processing', 2: 'Processed', 3: 'Failed' };
const STATUS_COLORS = { 0: 'var(--warning)', 1: 'var(--info)', 2: 'var(--success)', 3: 'var(--danger)' };

function Badge({ status }) {
    return React.createElement('span', {
        style: {
            display: 'inline-block', padding: '2px 10px', borderRadius: 12,
            fontSize: 12, fontWeight: 600, color: '#fff',
            background: STATUS_COLORS[status] || 'var(--text-muted)',
        }
    }, STATUS_LABELS[status] || status);
}

function StatCard({ label, value, color }) {
    return React.createElement('div', {
        style: {
            background: 'var(--surface)', border: '1px solid var(--border)',
            borderRadius: 8, padding: '20px 24px', flex: '1 1 0',
            minWidth: 140,
        }
    },
        React.createElement('div', { style: { fontSize: 13, color: 'var(--text-muted)', marginBottom: 4 } }, label),
        React.createElement('div', { style: { fontSize: 28, fontWeight: 700, color: color || 'var(--text)' } }, value)
    );
}

function StatsPanel({ stats }) {
    if (!stats) return React.createElement('div', null, 'Loading...');
    const { outbox, inbox, deadLetterCount } = stats;
    return React.createElement('div', null,
        React.createElement('h3', { style: { marginBottom: 12, fontSize: 16, color: 'var(--text-muted)' } }, 'Outbox'),
        React.createElement('div', { style: { display: 'flex', gap: 12, marginBottom: 20, flexWrap: 'wrap' } },
            React.createElement(StatCard, { label: 'Pending', value: outbox.pending, color: 'var(--warning)' }),
            React.createElement(StatCard, { label: 'Processing', value: outbox.processing, color: 'var(--info)' }),
            React.createElement(StatCard, { label: 'Processed', value: outbox.processed, color: 'var(--success)' }),
            React.createElement(StatCard, { label: 'Failed', value: outbox.failed, color: 'var(--danger)' }),
        ),
        React.createElement('h3', { style: { marginBottom: 12, fontSize: 16, color: 'var(--text-muted)' } }, 'Inbox'),
        React.createElement('div', { style: { display: 'flex', gap: 12, marginBottom: 20, flexWrap: 'wrap' } },
            React.createElement(StatCard, { label: 'Pending', value: inbox.pending, color: 'var(--warning)' }),
            React.createElement(StatCard, { label: 'Processing', value: inbox.processing, color: 'var(--info)' }),
            React.createElement(StatCard, { label: 'Processed', value: inbox.processed, color: 'var(--success)' }),
            React.createElement(StatCard, { label: 'Failed', value: inbox.failed, color: 'var(--danger)' }),
        ),
        React.createElement('div', { style: { display: 'flex', gap: 12, flexWrap: 'wrap' } },
            React.createElement(StatCard, { label: 'Dead Letter Queue', value: deadLetterCount, color: 'var(--danger)' }),
        ),
    );
}

function DataTable({ columns, rows, renderActions }) {
    return React.createElement('div', { style: { overflowX: 'auto' } },
        React.createElement('table', {
            style: {
                width: '100%', borderCollapse: 'collapse', fontSize: 13,
                background: 'var(--surface)', borderRadius: 8, overflow: 'hidden',
            }
        },
            React.createElement('thead', null,
                React.createElement('tr', null,
                    columns.map(col => React.createElement('th', {
                        key: col.key,
                        style: {
                            textAlign: 'left', padding: '10px 14px',
                            borderBottom: '1px solid var(--border)',
                            color: 'var(--text-muted)', fontWeight: 600, fontSize: 12,
                            whiteSpace: 'nowrap',
                        }
                    }, col.label)),
                    renderActions && React.createElement('th', {
                        style: {
                            padding: '10px 14px', borderBottom: '1px solid var(--border)',
                            color: 'var(--text-muted)', fontWeight: 600, fontSize: 12,
                        }
                    }, 'Actions'),
                ),
            ),
            React.createElement('tbody', null,
                rows.length === 0
                    ? React.createElement('tr', null,
                        React.createElement('td', {
                            colSpan: columns.length + (renderActions ? 1 : 0),
                            style: { padding: 20, textAlign: 'center', color: 'var(--text-muted)' }
                        }, 'No data'),
                    )
                    : rows.map((row, i) =>
                        React.createElement('tr', {
                            key: row.id || i,
                            style: { borderBottom: '1px solid var(--border)' }
                        },
                            columns.map(col =>
                                React.createElement('td', {
                                    key: col.key,
                                    style: {
                                        padding: '8px 14px', maxWidth: 280,
                                        overflow: 'hidden', textOverflow: 'ellipsis',
                                        whiteSpace: 'nowrap', fontSize: 13,
                                    }
                                }, col.render ? col.render(row[col.key], row) : String(row[col.key] ?? '')),
                            ),
                            renderActions && React.createElement('td', { style: { padding: '8px 14px' } }, renderActions(row)),
                        ),
                    ),
            ),
        ),
    );
}

function MessageListPage({ title, fetchFn, columns, renderActions }) {
    const [messages, setMessages] = useState([]);
    const [statusFilter, setStatusFilter] = useState(null);
    const [loading, setLoading] = useState(true);

    const load = useCallback(async () => {
        setLoading(true);
        try {
            const qs = statusFilter !== null ? `?status=${statusFilter}` : '';
            const data = await fetchFn(qs);
            setMessages(data);
        } catch { setMessages([]); }
        setLoading(false);
    }, [statusFilter, fetchFn]);

    useEffect(() => { load(); }, [load]);

    const filters = React.createElement('div', { style: { display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' } },
        [null, 0, 1, 2, 3].map(s =>
            React.createElement('button', {
                key: String(s),
                onClick: () => setStatusFilter(s),
                style: {
                    padding: '6px 14px', borderRadius: 6, border: '1px solid var(--border)',
                    background: statusFilter === s ? 'var(--primary)' : 'var(--surface)',
                    color: 'var(--text)', cursor: 'pointer', fontSize: 13,
                }
            }, s === null ? 'All' : STATUS_LABELS[s]),
        ),
        React.createElement('button', {
            onClick: load,
            style: {
                padding: '6px 14px', borderRadius: 6, border: '1px solid var(--border)',
                background: 'var(--surface)', color: 'var(--text)', cursor: 'pointer',
                fontSize: 13, marginLeft: 'auto',
            }
        }, 'Refresh'),
    );

    return React.createElement('div', null,
        React.createElement('h2', { style: { marginBottom: 16, fontSize: 20 } }, title),
        filters,
        loading
            ? React.createElement('div', { style: { color: 'var(--text-muted)' } }, 'Loading...')
            : React.createElement(DataTable, { columns, rows: messages, renderActions }),
    );
}

function App() {
    const [tab, setTab] = useState('stats');
    const [stats, setStats] = useState(null);

    useEffect(() => {
        apiFetch('/stats').then(setStats).catch(() => {});
        const interval = setInterval(() => {
            apiFetch('/stats').then(setStats).catch(() => {});
        }, 5000);
        return () => clearInterval(interval);
    }, []);

    const tabs = [
        { id: 'stats', label: 'Overview' },
        { id: 'outbox', label: 'Outbox' },
        { id: 'inbox', label: 'Inbox' },
        { id: 'deadletter', label: 'Dead Letter' },
        { id: 'handlers', label: 'Handlers' },
    ];

    const statusRender = (val) => React.createElement(Badge, { status: val });
    const dateRender = (val) => val ? new Date(val).toLocaleString() : '-';

    const outboxColumns = [
        { key: 'id', label: 'ID' },
        { key: 'eventType', label: 'Event Type' },
        { key: 'status', label: 'Status', render: statusRender },
        { key: 'sourceApp', label: 'Source App' },
        { key: 'targetApp', label: 'Target App' },
        { key: 'correlationId', label: 'Correlation ID' },
        { key: 'retryCount', label: 'Retries' },
        { key: 'creationTime', label: 'Created', render: dateRender },
        { key: 'processedTime', label: 'Processed', render: dateRender },
        { key: 'lastError', label: 'Error' },
    ];

    const inboxColumns = [
        { key: 'id', label: 'ID' },
        { key: 'eventType', label: 'Event Type' },
        { key: 'handlerType', label: 'Handler Type' },
        { key: 'status', label: 'Status', render: statusRender },
        { key: 'sourceApp', label: 'Source App' },
        { key: 'correlationId', label: 'Correlation ID' },
        { key: 'retryCount', label: 'Retries' },
        { key: 'creationTime', label: 'Created', render: dateRender },
        { key: 'processedTime', label: 'Processed', render: dateRender },
        { key: 'lastError', label: 'Error' },
    ];

    const dlColumns = [
        { key: 'id', label: 'ID' },
        { key: 'eventType', label: 'Event Type' },
        { key: 'handlerType', label: 'Handler Type' },
        { key: 'sourceApp', label: 'Source App' },
        { key: 'targetApp', label: 'Target App' },
        { key: 'correlationId', label: 'Correlation ID' },
        { key: 'totalRetryCount', label: 'Total Retries' },
        { key: 'failedTime', label: 'Failed', render: dateRender },
        { key: 'lastError', label: 'Error' },
        { key: 'isRequeued', label: 'Requeued', render: (v) => v ? 'Yes' : 'No' },
        { key: 'requeuedTime', label: 'Requeued At', render: dateRender },
    ];

    const handleRequeue = async (row) => {
        try {
            await apiFetch(`/deadletter/${row.id}/requeue`, { method: 'POST' });
            alert('Message requeued successfully');
        } catch {
            alert('Failed to requeue message');
        }
    };

    const [handlers, setHandlers] = useState([]);
    useEffect(() => {
        if (tab === 'handlers') {
            apiFetch('/handlers').then(setHandlers).catch(() => setHandlers([]));
        }
    }, [tab]);

    let content;
    switch (tab) {
        case 'stats':
            content = React.createElement(StatsPanel, { stats });
            break;
        case 'outbox':
            content = React.createElement(MessageListPage, {
                title: 'Outbox Messages',
                fetchFn: (qs) => apiFetch('/outbox' + qs),
                columns: outboxColumns,
            });
            break;
        case 'inbox':
            content = React.createElement(MessageListPage, {
                title: 'Inbox Messages',
                fetchFn: (qs) => apiFetch('/inbox' + qs),
                columns: inboxColumns,
            });
            break;
        case 'deadletter':
            content = React.createElement(MessageListPage, {
                title: 'Dead Letter Queue',
                fetchFn: (qs) => apiFetch('/deadletter' + qs),
                columns: dlColumns,
                renderActions: (row) =>
                    !row.isRequeued && React.createElement('button', {
                        onClick: () => handleRequeue(row),
                        style: {
                            padding: '4px 12px', borderRadius: 6,
                            border: '1px solid var(--warning)',
                            background: 'transparent', color: 'var(--warning)',
                            cursor: 'pointer', fontSize: 12, fontWeight: 600,
                        }
                    }, 'Requeue'),
            });
            break;
        case 'handlers':
            content = React.createElement('div', null,
                React.createElement('h2', { style: { marginBottom: 16, fontSize: 20 } }, 'Registered Handlers'),
                React.createElement(DataTable, {
                    columns: [
                        { key: 'eventType', label: 'Event Type' },
                        { key: 'handlerType', label: 'Handler Type' },
                    ],
                    rows: handlers,
                }),
            );
            break;
    }

    return React.createElement('div', { style: { maxWidth: 1400, margin: '0 auto', padding: '24px 32px' } },
        React.createElement('header', {
            style: {
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                marginBottom: 32, paddingBottom: 16, borderBottom: '1px solid var(--border)',
            }
        },
            React.createElement('h1', { style: { fontSize: 22, fontWeight: 700 } }, 'Eksen EventBus Dashboard'),
        ),
        React.createElement('nav', {
            style: { display: 'flex', gap: 4, marginBottom: 24 }
        },
            tabs.map(t => React.createElement('button', {
                key: t.id,
                onClick: () => setTab(t.id),
                style: {
                    padding: '8px 20px', borderRadius: 6, border: 'none',
                    background: tab === t.id ? 'var(--primary)' : 'transparent',
                    color: tab === t.id ? '#fff' : 'var(--text-muted)',
                    cursor: 'pointer', fontSize: 14, fontWeight: 500,
                    transition: 'all 0.15s',
                }
            }, t.label)),
        ),
        content,
    );
}

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(React.createElement(App));
