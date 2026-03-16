window.plaidLink = {
    open: async function (dotNetRef) {
        try {
            const response = await fetch('/api/plaid/create-link-token', { method: 'POST' });
            const data = await response.json();

            const handler = Plaid.create({
                token: data.linkToken,
                onSuccess: async function (publicToken, metadata) {
                    const institution = metadata.institution;
                    await fetch('/api/plaid/exchange-token', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            publicToken: publicToken,
                            institutionName: institution.name,
                            institutionId: institution.institution_id
                        })
                    });
                    dotNetRef.invokeMethodAsync('OnPlaidSuccess');
                },
                onExit: function (err) {
                    if (err) {
                        dotNetRef.invokeMethodAsync('OnPlaidError', err.error_message || 'Connection cancelled');
                    }
                }
            });

            handler.open();
        } catch (e) {
            dotNetRef.invokeMethodAsync('OnPlaidError', e.message || 'Failed to initialize Plaid Link');
        }
    },

    handleOAuthCallback: async function (currentUri) {
        try {
            const response = await fetch('/api/plaid/create-link-token', { method: 'POST' });
            const data = await response.json();

            const handler = Plaid.create({
                token: data.linkToken,
                receivedRedirectUri: currentUri,
                onSuccess: async function (publicToken, metadata) {
                    const institution = metadata.institution;
                    await fetch('/api/plaid/exchange-token', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            publicToken: publicToken,
                            institutionName: institution.name,
                            institutionId: institution.institution_id
                        })
                    });
                    window.location.href = '/budget';
                },
                onExit: function (err) {
                    window.location.href = '/budget';
                }
            });

            handler.open();
        } catch (e) {
            console.error('Plaid OAuth callback error:', e);
            window.location.href = '/budget';
        }
    }
};
