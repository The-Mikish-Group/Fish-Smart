/**
 * Auto-dismiss success alerts after a specified time
 * This script automatically closes all success alerts after 5 seconds
 */

document.addEventListener('DOMContentLoaded', function() {
    // Configuration
    const AUTO_DISMISS_DELAY = 5000; // 5 seconds
    
    function autoDismissSuccessAlerts() {
        const successAlerts = document.querySelectorAll('.alert-success.alert-dismissible');
        
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
    }
    
    // Auto-dismiss existing alerts
    autoDismissSuccessAlerts();
    
    // Watch for dynamically added alerts (e.g., via AJAX)
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.type === 'childList') {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        // Check if the added node or its children contain success alerts
                        const newAlerts = node.classList && node.classList.contains('alert-success') 
                            ? [node] 
                            : node.querySelectorAll ? node.querySelectorAll('.alert-success.alert-dismissible') : [];
                        
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