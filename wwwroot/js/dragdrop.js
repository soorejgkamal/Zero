'use strict';

(function () {
    let _dotNetRef    = null;
    let _mouseDragCardId = null;
    let _dragging     = null;   // active touch drag state
    let _touchPending = null;   // touch started but not yet confirmed as drag
    let _initialized  = false;

    const DRAG_THRESHOLD = 10;  // px finger must move before drag starts

    window.cardDragDrop = {
        init: function (dotNetRef) {
            _dotNetRef = dotNetRef;
            if (_initialized) return;
            _initialized = true;

            document.addEventListener('dragstart', onMouseDragStart);
            document.addEventListener('dragover',  onMouseDragOver);
            document.addEventListener('drop',      onMouseDrop);
            document.addEventListener('dragend',   onMouseDragEnd);

            // passive:false required so we can preventDefault in touchmove once drag starts
            document.addEventListener('touchstart',  onTouchStart,  { passive: true  });
            document.addEventListener('touchmove',   onTouchMove,   { passive: false });
            document.addEventListener('touchend',    onTouchEnd,    { passive: true  });
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
    // Strategy: don't prevent default on touchstart so taps still fire
    // click events normally. Only commit to a drag once the finger has
    // moved past DRAG_THRESHOLD — then take over touch handling.

    function onTouchStart(e) {
        const el = e.target.closest('[data-card-id][draggable="true"]');
        if (!el) return;
        const touch = e.touches[0];
        _touchPending = {
            el,
            cardId: el.dataset.cardId,
            startX: touch.clientX,
            startY: touch.clientY,
        };
        // No preventDefault here — tap clicks must still work
    }

    function onTouchMove(e) {
        if (_touchPending) {
            const touch = e.touches[0];
            const dx = touch.clientX - _touchPending.startX;
            const dy = touch.clientY - _touchPending.startY;
            if (Math.sqrt(dx * dx + dy * dy) >= DRAG_THRESHOLD) {
                // Finger moved enough — promote to a real drag
                startTouchDrag(_touchPending.el, touch);
                _touchPending = null;
            }
        }

        if (_dragging) {
            e.preventDefault(); // stop scroll while dragging
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
        }
    }

    function startTouchDrag(el, touch) {
        const rect = el.getBoundingClientRect();
        const ghost = el.cloneNode(true);
        Object.assign(ghost.style, {
            position:      'fixed',
            left:          rect.left   + 'px',
            top:           rect.top    + 'px',
            width:         rect.width  + 'px',
            height:        rect.height + 'px',
            opacity:       '0.88',
            pointerEvents: 'none',
            zIndex:        '9999',
            transform:     'scale(1.1) rotate(4deg)',
            boxShadow:     '0 14px 36px rgba(0,0,0,0.55)',
            transition:    'none',
            margin:        '0',
        });
        document.body.appendChild(ghost);
        _dragging = {
            cardId:  el.dataset.cardId,
            ghost,
            offsetX: touch.clientX - rect.left,
            offsetY: touch.clientY - rect.top,
        };
    }

    function onTouchEnd(e) {
        _touchPending = null;
        if (!_dragging) return;

        const touch = e.changedTouches[0];
        const zone  = document.getElementById('table-drop-zone');
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
        _touchPending = null;
        if (_dragging) {
            _dragging.ghost.remove();
            _dragging = null;
        }
        const zone = document.getElementById('table-drop-zone');
        if (zone) zone.classList.remove('drop-zone-active');
    }
})();
