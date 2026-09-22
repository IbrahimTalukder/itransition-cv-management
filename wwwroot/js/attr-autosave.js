function initAttrAutosave(cloudName, uploadPreset) {
    const token = document.querySelector('input[name=__RequestVerificationToken]')?.value;

    async function saveAttr(field, payloadExtra) {
        const payload = {
            attributeDefinitionId: parseInt(field.dataset.attrId, 10),
            stringValue: null, textValue: null, numericValue: null, dateValue: null,
            booleanValue: null, selectedOptionId: null, periodStart: null, periodEnd: null, imageUrl: null,
            version: field.dataset.version ? parseInt(field.dataset.version, 10) : null,
            ...payloadExtra
        };
        const status = field.querySelector('.attr-save-status');
        const res = await fetch('/Profile/AutoSaveAttribute', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(payload)
        });
        if (res.status === 409) {
            if (status) status.textContent = 'Conflict — reloading…';
            setTimeout(() => location.reload(), 1200);
        } else if (res.status === 400) {
            const data = await res.json();
            if (status) { status.textContent = data.message; status.classList.add('text-danger'); }
        } else if (res.ok) {
            const data = await res.json();
            field.dataset.version = data.version;
            if (status) { status.classList.remove('text-danger'); status.textContent = 'Saved'; setTimeout(() => status.textContent = '', 1500); }
        }
    }

    document.querySelectorAll('.attr-field').forEach(field => {
        const type = field.dataset.type;

        if (type === 'Text') {
            field.querySelector('textarea').addEventListener('blur', e => saveAttr(field, { textValue: e.target.value }));
        } else if (type === 'Numeric') {
            field.querySelector('input').addEventListener('blur', e =>
                saveAttr(field, { numericValue: e.target.value === '' ? null : parseFloat(e.target.value) }));
        } else if (type === 'Date') {
            field.querySelector('input').addEventListener('change', e =>
                saveAttr(field, { dateValue: e.target.value || null }));
        } else if (type === 'Period') {
            const save = () => saveAttr(field, {
                periodStart: field.querySelector('.period-start').value || null,
                periodEnd: field.querySelector('.period-end').value || null
            });
            field.querySelectorAll('input').forEach(i => i.addEventListener('change', save));
        } else if (type === 'Boolean') {
            field.querySelector('input[type=checkbox]').addEventListener('change', e =>
                saveAttr(field, { booleanValue: e.target.checked }));
        } else if (type === 'OneOfMany') {
            field.querySelector('select').addEventListener('change', e =>
                saveAttr(field, { selectedOptionId: e.target.value ? parseInt(e.target.value, 10) : null }));
        } else if (type === 'Image') {
            const dropzone = field.querySelector('.attr-image-dropzone');
            const input = field.querySelector('.attr-image-input');
            async function upload(file) {
                const fd = new FormData();
                fd.append('file', file); fd.append('upload_preset', uploadPreset);
                const res = await fetch(`https://api.cloudinary.com/v1_1/${cloudName}/image/upload`, { method: 'POST', body: fd });
                const data = await res.json();
                if (data.secure_url) {
                    saveAttr(field, { imageUrl: data.secure_url });
                    dropzone.innerHTML = `<img src="${data.secure_url}" style="max-height:60px" class="d-block mx-auto mb-1" />Uploaded`;
                }
            }
            dropzone.addEventListener('click', () => input.click());
            input.addEventListener('change', () => input.files[0] && upload(input.files[0]));
            dropzone.addEventListener('dragover', e => e.preventDefault());
            dropzone.addEventListener('drop', e => { e.preventDefault(); e.dataTransfer.files[0] && upload(e.dataTransfer.files[0]); });
        } else { 
            field.querySelector('input').addEventListener('blur', e => saveAttr(field, { stringValue: e.target.value }));
        }
    });
}
