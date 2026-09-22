document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('th[data-sort]').forEach((th, index) => {
        th.addEventListener('click', () => {
            const table = th.closest('table');
            const tbody = table.querySelector('tbody');
            const rows = Array.from(tbody.querySelectorAll('tr'));
            const asc = th.dataset.dir !== 'asc';
            th.dataset.dir = asc ? 'asc' : 'desc';
            const colIndex = Array.from(th.parentElement.children).indexOf(th);
            rows.sort((a, b) => {
                const av = a.children[colIndex]?.innerText.trim() ?? '';
                const bv = b.children[colIndex]?.innerText.trim() ?? '';
                return asc ? av.localeCompare(bv) : bv.localeCompare(av);
            });
            rows.forEach(r => tbody.appendChild(r));
        });
    });
});

function connectDiscussion(positionId, onNewPost) {
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/discussion').build();
    connection.on('NewPost', onNewPost);
    connection.start().then(() => connection.invoke('JoinPositionGroup', positionId));
    return connection;
}
