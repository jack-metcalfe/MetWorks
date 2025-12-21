#!/bin/bash

# CPU Usage Monitoring Script
# Displays CPU usage and alerts if threshold is exceeded

THRESHOLD=${1:-80}
INTERVAL=${2:-5}
DURATION=${3:-0}

echo "CPU Usage Monitor"
echo "================="
echo "Threshold: ${THRESHOLD}%"
echo "Interval: ${INTERVAL}s"
echo "Duration: ${DURATION}s (0 = infinite)"
echo ""

elapsed=0

while true; do
    # Get current CPU usage (Linux)
    if command -v top &> /dev/null; then
        cpu_usage=$(top -bn1 | grep "Cpu(s)" | awk '{print 100 - $8}' | cut -d'.' -f1)
    else
        # Fallback for macOS
        cpu_usage=$(ps aux | awk 'BEGIN {sum=0} {sum+=$3} END {print int(sum)}')
    fi
    
    timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    
    # Display usage
    printf "[%s] CPU: %3d%%" "$timestamp" "$cpu_usage"
    
    # Alert if threshold exceeded
    if [ "$cpu_usage" -ge "$THRESHOLD" ]; then
        printf " ⚠️  ALERT - CPU usage exceeded ${THRESHOLD}%%\n"
    else
        printf "\n"
    fi
    
    # Check if duration limit reached
    if [ "$DURATION" -gt 0 ] && [ "$elapsed" -ge "$DURATION" ]; then
        break
    fi
    
    elapsed=$((elapsed + INTERVAL))
    sleep "$INTERVAL"
done

echo ""
echo "Monitoring stopped."
