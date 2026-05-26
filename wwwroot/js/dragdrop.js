'use strict';

(function () {
    let _dotNetRef = null;
    let _mouseDragCardId = null;
    let _dragging = null; // touch drag state
    let _initialized = false;

    window.cardDragDrop = {
        init: function (dotNetRef) {
            _dotNetRef = dotNetRef;
            if (_initialized) return;
            _initialized = true;

            document.addEventListener('dragstart', onMouseDragStart);
            document.addEventListener('dragover', onMouseDragOver);
            document.addEventListener('drop', onMouseDrop);
            document.addEventListener('dragend', onMouseDragEnd);

            document.addEventListener('touchstart', onTouchStart, { passive: false });
            document.addEventListener('touchmove', onTouchMove, { passive: false });
            document.addEventListener('touchend', onTouchEnd, { passive: false });
            document.addEventListener('touchcancel', cancelTouchDrag);
        },
        dispose: function () {
            cancelTouchDrag();
            _dotNetRef = null;
        }
    };

    // ── Mouse Drag (HTML5 Drag API) ───────────────────────────────

    function onMouseDragStart(e) {
        const el = e.target.closest('[data-card-id][draggable="true"]');
        if (!el) return;
        _mouseDragCardId = el.dataset.cardId;
        if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
    }

    function onMouseDragOver(e) {
        const zone = document.getElementById('table-drop-zone');
        if (!zone || !_mouseDragCardId) return;
        const zr = zone.getBoundingClientRect();
        if (e.clientX >= zr.left && e.clientX <= zr.right &&
            e.clientY >= zr.top  && e.clientY <= zr.bottom) {
            e.preventDefault();
            zone.classList.add('drop-zone-active');
        } else {
            zone.classList.remove('drop-zone-active');
        }
    }

    function onMouseDrop(e) {
        const zone = document.getElementById('table-drop-zone');
        if (!zone) return;
        zone.classList.remove('drop-zone-active');
        const zr = zone.getBoundingClientRect();
        if (e.clientX >= zr.left && e.clientX <= zr.right &&
            e.clientY >= zr.top  && e.clientY <= zr.bottom &&
            _mouseDragCardId && _dotNetRef) {
            e.preventDefault();
            _dotNetRef.invokeMethodAsync('OnCardDroppedToDiscard', _mouseDragCardId);
        }
        _mouseDragCardId = null;
    }

    function onMouseDragEnd() {
        _mouseDragCardId = null;
        const zone = document.getElementById('table-drop-zone');
        if (zone) zone.classList.remove('drop-zone-active');
    }

    // ── Touch Drag ───────────────────────────────────────────────

    function onTouchStart(e) {
        const el = e.target.closest('[data-card-id][draggable="true"]');
        if (!el) return;

        const cardId = el.dataset.cardId;
        const touch = e.touches[0];
        const rect = el.getBoundingClientRect();

        const ghost = el.cloneNode(true);
        Object.assign(ghost.style, {
            position:     'fixed',
            left:         rect.left + 'px',
            top:          rect.top  + 'px',
            width:        rect.width  + 'px',
            height:       rect.height + 'px',
            opacity:      '0.88',
            pointerEvents:'none',
            zIndex:       '9999',
            transform:    'scale(1.1) rotate(4deg)',
            boxShadow:    '0 14px 36px rgba(0,0,0,0.55)',
            transition:   'none',
            margin:       '0',
        });
        document.body.appendChild(ghost);

        _dragging = {
            cardId,
            ghost,
            offsetX: touch.clientX - rect.left,
            offsetY: touch.clientY - rect.top,
        };

        e.preventDefault();
    }

    function onTouchMove(e) {
        if (!_dragging) return;
        const touch = e.touches[0];
        _dragging.ghost.style.left = (touch.clientX - _dragging.offsetX) + 'px';
        _dragging.ghost.style.top  = (touch.clientY - _dragging.offsetY) + 'px';

        const zone = document.getElementById('table-drop-zone');
        if (zone) {
            const zr = zone.getBoundingClientRect();
            const over = touch.clientX >= zr.left && touch.clientX <= zr.right &&
                         touch.clientY >= zr.top  && touch.clientY <= zr.bottom;
            zone.classList.toggle('drop-zone-active', over);
        }
        e.preventDefault();
    }

    function onTouchEnd(e) {
        if (!_dragging) return;
        const touch = e.changedTouches[0];
        const zone = document.getElementById('table-drop-zone');
        let dropped = false;

        if (zone) {
            const zr = zone.getBoundingClientRect();
            dropped = touch.clientX >= zr.left && touch.clientX <= zr.right &&
                      touch.clientY >= zr.top  && touch.clientY <= zr.bottom;
            zone.classList.remove('drop-zone-active');
        }

        _dragging.ghost.remove();
        const cardId = _dragging.cardId;
        _dragging = null;

        if (dropped && _dotNetRef) {
            _dotNetRef.invokeMethodAsync('OnCardDroppedToDiscard', cardId);
        }
    }

    function cancelTouchDrag() {
        if (_dragging) {
            _dragging.ghost.remove();
            _dragging = null;
        }
        const zone = document.getElementById('table-drop-zone');
        if (zone) zone.classList.remove('drop-zone-active');
    }
})();
