"use client";

import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
    Box,
    Typography,
    List,
    ListItem,
    ListItemIcon,
    ListItemText,
    Checkbox,
    Collapse,
    IconButton,
    TextField,
    Stack,
    Divider,
    Paper,
    Grid,
    Card,
    CardContent
} from '@mui/material';
import {
    ExpandMore as ExpandMoreIcon,
    ChevronRight as ChevronRightIcon
} from '@mui/icons-material';
import { catalogApi } from '../services/api';

interface ServiceNode {
    id: string;
    name: string;
    level: string;
    price: { local: number; usdRef: number };
    children: ServiceNode[];
}

interface ServiceSelectionProps {
    regionId: string;
    selectedItems: any[];
    onChange: (updater: (prev: any[]) => any[]) => void;
}

// Sub-component for a single service row to avoid full tree re-renders
function ServiceRow({
    node,
    depth,
    isSelected,
    onToggle,
    onSelect,
    onUpdate,
    expanded,
    quantity,
    children
}: any) {
    const hasChildren = node.children && node.children.length > 0;
    const isNodeExpanded = expanded.includes(node.id);

    return (
        <>
            <ListItem
                sx={{ pl: depth * 4 }}
                secondaryAction={
                    isSelected && (
                        <Stack direction="row" spacing={2} alignItems="center">
                            <TextField
                                size="small"
                                type="number"
                                label="Qty"
                                value={quantity}
                                onChange={(e) => {
                                    const val = e.target.value === '' ? 0 : parseFloat(e.target.value);
                                    if (!isNaN(val)) onUpdate(node.id, 'quantity', val);
                                }}
                                onClick={(e) => e.stopPropagation()}
                                sx={{ width: 80 }}
                            />
                            <Typography variant="body2" color="text.secondary">
                                {node.price?.local?.toLocaleString() || 0} VND
                            </Typography>
                        </Stack>
                    )
                }
            >
                <ListItemIcon>
                    {hasChildren ? (
                        <IconButton size="small" onClick={() => onToggle(node.id)}>
                            {isNodeExpanded ? <ExpandMoreIcon /> : <ChevronRightIcon />}
                        </IconButton>
                    ) : <Box sx={{ width: 40 }} />}
                    <Checkbox
                        checked={isSelected}
                        onChange={() => onSelect(node)}
                    />
                </ListItemIcon>
                <ListItemText
                    primary={node.name}
                    secondary={node.level}
                    primaryTypographyProps={{ fontWeight: node.level === 'MAIN' ? 'bold' : 'normal' }}
                />
            </ListItem>
            {hasChildren && (
                <Collapse in={isNodeExpanded} timeout="auto" unmountOnExit>
                    <List component="div" disablePadding>
                        {children}
                    </List>
                </Collapse>
            )}
        </>
    );
}

export default function ServiceSelection({ regionId, selectedItems, onChange }: ServiceSelectionProps) {
    const [tree, setTree] = useState<ServiceNode[]>([]);
    const [expanded, setExpanded] = useState<string[]>([]);

    useEffect(() => {
        if (regionId) {
            catalogApi.getServiceTree(regionId).then((res: any) => setTree(res.data.items));
        }
    }, [regionId]);

    const handleToggle = useCallback((id: string) => {
        setExpanded((prev) =>
            prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
        );
    }, []);

    const handleSelect = useCallback((service: ServiceNode) => {
        onChange((prev) => {
            const exists = prev.find(i => i.serviceId === service.id);
            if (exists) {
                return prev.filter(i => i.serviceId !== service.id);
            } else {
                return [...prev, {
                    serviceId: service.id,
                    quantity: 1,
                    name: service.name,
                    price: service.price || { local: 0, usdRef: 0 },
                    notes: ''
                }];
            }
        });
    }, [onChange]);

    const handleUpdate = useCallback((id: string, field: string, value: any) => {
        console.log(`[ServiceSelection] Updating ${id}: ${field} = ${value}`);
        onChange((prev) => prev.map(item =>
            item.serviceId === id ? { ...item, [field]: value } : item
        ));
    }, [onChange]);

    const renderTree = (nodes: ServiceNode[], depth = 0): React.ReactNode[] => {
        return nodes.map(node => {
            const item = selectedItems.find(i => i.serviceId === node.id);
            return (
                <ServiceRow
                    key={node.id}
                    node={node}
                    depth={depth}
                    isSelected={!!item}
                    quantity={item?.quantity ?? 1}
                    expanded={expanded}
                    onToggle={handleToggle}
                    onSelect={handleSelect}
                    onUpdate={handleUpdate}
                >
                    {node.children && renderTree(node.children, depth + 1)}
                </ServiceRow>
            );
        });
    };

    const totalCost = useMemo(() => {
        return selectedItems.reduce((acc, i) => acc + (Number(i.quantity) * (i.price?.local || 0)), 0);
    }, [selectedItems]);

    return (
        <Box>
            <Typography variant="h6" gutterBottom>Select Services for Proposal</Typography>
            <Paper variant="outlined" sx={{ mb: 4 }}>
                <List>
                    {renderTree(tree)}
                </List>
            </Paper>

            <Divider />

            <Box sx={{ mt: 4 }}>
                <Typography variant="h6" gutterBottom>Selected Items Summary</Typography>
                {selectedItems.length === 0 && <Typography color="text.secondary">No items selected yet.</Typography>}
                <Stack spacing={2}>
                    {selectedItems.map(item => (
                        <Card variant="outlined" key={item.serviceId}>
                            <CardContent sx={{ py: 1, '&:last-child': { pb: 1 } }}>
                                <Grid container spacing={2} alignItems="center">
                                    <Grid size={6}>
                                        <Typography variant="subtitle1" fontWeight="medium">{item.name}</Typography>
                                    </Grid>
                                    <Grid size={3}>
                                        <TextField
                                            size="small"
                                            label="Notes"
                                            fullWidth
                                            value={item.notes || ''}
                                            onChange={(e) => handleUpdate(item.serviceId, 'notes', e.target.value)}
                                        />
                                    </Grid>
                                    <Grid size={3} sx={{ textAlign: "right" }}>
                                        <Typography fontWeight="bold">
                                            {item.quantity} x {(item.price?.local || 0).toLocaleString()} VND
                                        </Typography>
                                    </Grid>
                                </Grid>
                            </CardContent>
                        </Card>
                    ))}
                    {selectedItems.length > 0 && (
                        <Box textAlign="right" sx={{ mt: 2 }}>
                            <Typography variant="h5" color="primary.main" fontWeight="bold">
                                Total: {totalCost.toLocaleString()} VND
                            </Typography>
                        </Box>
                    )}
                </Stack>
            </Box>
        </Box>
    );
}
