/**
 * Auto-dismiss success alerts and toasts after a specified time
 * This script automatically closes all success alerts and toasts after 4 seconds
 */

document.addEventListener('DOMContentLoaded', function() {
    // Configuration
    const AUTO_DISMISS_DELAY = 4000; // 4 seconds
    
    function autoDismissSuccessAlerts() {
        const successAlerts = document.querySelectorAll('.alert-success.alert-dismissible');
        const successToasts = document.querySelectorAll('.toast');
        
        successAlerts.forEach(function(alert) {
            // Only auto-dismiss if it hasn't been dismissed already
            if (alert.style.display !== 'none' && !alert.classList.contains('auto-dismiss-processed')) {
                // Mark as processed to avoid duplicate timers
                alert.classList.add('auto-dismiss-processed');
                
                setTimeout(function() {
                    // Check if the alert still exists and is visible
                    if (alert && alert.parentNode && !alert.classList.contains('d-none')) {
                        try {
                            const bsAlert = new bootstrap.Alert(alert);
                            bsAlert.close();
                        } catch (e) {
                            // Fallback: remove the alert manually if Bootstrap fails
                            alert.style.transition = 'opacity 0.15s linear';
                            alert.style.opacity = '0';
                            setTimeout(() => {
                                if (alert.parentNode) {
                                    alert.parentNode.removeChild(alert);
                                }
                            }, 150);
                        }
                    }
                }, AUTO_DISMISS_DELAY);
            }
        });
        
        // Handle success toasts
        successToasts.forEach(function(toast) {
            // Only auto-dismiss if it hasn't been processed already
            if (!toast.classList.contains('auto-dismiss-processed')) {
                // Mark as processed to avoid duplicate timers
                toast.classList.add('auto-dismiss-processed');
                
                try {
                    const bsToast = new bootstrap.Toast(toast, {
                        autohide: true,
                        delay: AUTO_DISMISS_DELAY
                    });
                    bsToast.show();
                } catch (e) {
                    // Fallback: manually hide the toast if Bootstrap fails
                    setTimeout(function() {
                        if (toast && toast.parentNode) {
                            toast.style.transition = 'opacity 0.15s linear';
                            toast.style.opacity = '0';
                            setTimeout(() => {
                                if (toast.parentNode) {
                                    toast.parentNode.removeChild(toast);
                                }
                            }, 150);
                        }
                    }, AUTO_DISMISS_DELAY);
                }
            }
        });
    }
    
    // Auto-dismiss existing alerts and toasts
    autoDismissSuccessAlerts();
    
    // Watch for dynamically added alerts (e.g., via AJAX)
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.type === 'childList') {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        // Check if the added node or its children contain success alerts or toasts
                        const newAlerts = node.classList && node.classList.contains('alert-success') 
                            ? [node] 
                            : node.querySelectorAll ? node.querySelectorAll('.alert-success.alert-dismissible') : [];
                        
                        const newToasts = node.classList && node.classList.contains('toast')
                            ? [node]
                            : node.querySelectorAll ? node.querySelectorAll('.toast') : [];
                        
                        newAlerts.forEach(function(alert) {
                            if (!alert.classList.contains('auto-dismiss-processed')) {
                                alert.classList.add('auto-dismiss-processed');
                                setTimeout(function() {
                                    if (alert && alert.parentNode && !alert.classList.contains('d-none')) {
                                        try {
                                            const bsAlert = new bootstrap.Alert(alert);
                                            bsAlert.close();
                                        } catch (e) {
                                            alert.style.transition = 'opacity 0.15s linear';
                                            alert.style.opacity = '0';
                                            setTimeout(() => {
                                                if (alert.parentNode) {
                                                    alert.parentNode.removeChild(alert);
                                                }
                                            }, 150);
                                        }
                                    }
                                }, AUTO_DISMISS_DELAY);
                            }
                        });
                        
                        newToasts.forEach(function(toast) {
                            if (!toast.classList.contains('auto-dismiss-processed')) {
                                toast.classList.add('auto-dismiss-processed');
                                try {
                                    const bsToast = new bootstrap.Toast(toast, {
                                        autohide: true,
                                        delay: AUTO_DISMISS_DELAY
                                    });
                                    bsToast.show();
                                } catch (e) {
                                    setTimeout(function() {
                                        if (toast && toast.parentNode) {
                                            toast.style.transition = 'opacity 0.15s linear';
                                            toast.style.opacity = '0';
                                            setTimeout(() => {
                                                if (toast.parentNode) {
                                                    toast.parentNode.removeChild(toast);
                                                }
                                            }, 150);
                                        }
                                    }, AUTO_DISMISS_DELAY);
                                }
                            }
                        });
                    }
                });
            }
        });
    });
    
    // Start observing changes in the document
    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
});