"use client";

import React, { useEffect, useState } from 'react';
import {
    Box,
    Container,
    Typography,
    Paper,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    Button,
    Chip,
    IconButton,
    Stack,
    CircularProgress
} from '@mui/material';
import {
    Visibility as ViewIcon,
    Add as AddIcon,
    Edit as EditIcon,
    Delete as DeleteIcon,
    DeleteSweep as BulkDeleteIcon
} from '@mui/icons-material';
import Link from 'next/link';
import { proposalApi } from '../services/api';
import { Checkbox } from '@mui/material';

export default function ProposalsList() {
    const [proposals, setProposals] = useState<any[]>([]);
    const [selectedIds, setSelectedIds] = useState<string[]>([]);
    const [loading, setLoading] = useState(true);
    const [user, setUser] = useState<any>(null);

    const fetchProposals = async () => {
        setLoading(true);
        try {
            const res = await proposalApi.list();
            setProposals(res.data.items);
            setSelectedIds([]); // Reset selection after fetch
        } catch (err) {
            console.error(err);
        } finally {
            setLoading(false);
        }
    };

    const handleDelete = async (id: string) => {
        if (window.confirm('Are you sure you want to delete this proposal? This action cannot be undone.')) {
            try {
                await proposalApi.delete(id);
                fetchProposals();
            } catch (err) {
                console.error(err);
                alert('Failed to delete proposal');
            }
        }
    };

    const handleBatchDelete = async () => {
        if (window.confirm(`Are you sure you want to delete ${selectedIds.length} selected proposals? This action cannot be undone.`)) {
            try {
                setLoading(true);
                await proposalApi.batchDelete(selectedIds);
                fetchProposals();
            } catch (err) {
                console.error(err);
                alert('Failed to delete proposals');
                setLoading(false);
            }
        }
    };

    const toggleSelectAll = (checked: boolean) => {
        if (checked) {
            setSelectedIds(proposals.map(p => p.id));
        } else {
            setSelectedIds([]);
        }
    };

    const toggleSelect = (id: string) => {
        setSelectedIds(prev =>
            prev.includes(id)
                ? prev.filter(item => item !== id)
                : [...prev, id]
        );
    };

    useEffect(() => {
        const storedUser = localStorage.getItem('user');
        if (storedUser) setUser(JSON.parse(storedUser));
        fetchProposals();
    }, []);

    if (loading && proposals.length === 0) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;

    const allSelected = proposals.length > 0 && selectedIds.length === proposals.length;
    const someSelected = selectedIds.length > 0 && selectedIds.length < proposals.length;

    const isAdmin = user?.roles?.includes('Admin');
    const isCreatorFull = user?.roles?.includes('ProposalCreator') || isAdmin;

    return (
        <Container maxWidth="lg" sx={{ py: 8 }}>
            <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 4 }}>
                <Typography variant="h4" fontWeight="bold">My Proposals</Typography>
                <Stack direction="row" spacing={2}>
                    {isCreatorFull && selectedIds.length > 0 && (
                        <Button
                            variant="outlined"
                            color="error"
                            startIcon={<BulkDeleteIcon />}
                            onClick={handleBatchDelete}
                            disabled={loading}
                        >
                            Delete Selected ({selectedIds.length})
                        </Button>
                    )}
                    {isCreatorFull && (
                        <Button
                            variant="contained"
                            startIcon={<AddIcon />}
                            component={Link}
                            href="/proposals/new"
                        >
                            New Proposal
                        </Button>
                    )}
                </Stack>
            </Stack>

            <TableContainer component={Paper}>
                <Table>
                    <TableHead>
                        <TableRow>
                            {isCreatorFull && (
                                <TableCell padding="checkbox">
                                    <Checkbox
                                        indeterminate={someSelected}
                                        checked={allSelected}
                                        onChange={(e) => toggleSelectAll(e.target.checked)}
                                    />
                                </TableCell>
                            )}
                            <TableCell>Client Name</TableCell>
                            <TableCell>Region</TableCell>
                            <TableCell>Status</TableCell>
                            <TableCell>Last Updated</TableCell>
                            <TableCell align="right">Actions</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {proposals.length === 0 ? (
                            <TableRow>
                                <TableCell colSpan={isCreatorFull ? 6 : 5} align="center">No proposals found. Start by creating one!</TableCell>
                            </TableRow>
                        ) : (
                            proposals.map((p) => {
                                const isSelected = selectedIds.includes(p.id);
                                return (
                                    <TableRow key={p.id} selected={isCreatorFull && isSelected}>
                                        {isCreatorFull && (
                                            <TableCell padding="checkbox">
                                                <Checkbox
                                                    checked={isSelected}
                                                    onChange={() => toggleSelect(p.id)}
                                                />
                                            </TableCell>
                                        )}
                                        <TableCell sx={{ fontWeight: 'medium' }}>{p.clientName}</TableCell>
                                        <TableCell>{p.regionName}</TableCell>
                                        <TableCell>
                                            <Chip
                                                label={p.status}
                                                size="small"
                                                color={p.status === 'GENERATED' ? 'success' : 'warning'}
                                            />
                                        </TableCell>
                                        <TableCell>{new Date(p.updatedAt).toLocaleDateString()}</TableCell>
                                        <TableCell align="right">
                                            <Stack direction="row" spacing={1} justifyContent="flex-end">
                                                <IconButton component={Link} href={`/proposals/${p.id}`} title="View Details">
                                                    <ViewIcon color="primary" />
                                                </IconButton>
                                                {isCreatorFull && (
                                                    <IconButton component={Link} href={`/proposals/new?id=${p.id}`} title="Edit Proposal">
                                                        <EditIcon color="action" />
                                                    </IconButton>
                                                )}
                                                {isCreatorFull && (
                                                    <IconButton onClick={() => handleDelete(p.id)} title="Delete Proposal">
                                                        <DeleteIcon color="error" />
                                                    </IconButton>
                                                )}
                                            </Stack>
                                        </TableCell>
                                    </TableRow>
                                );
                            })
                        )}
                    </TableBody>
                </Table>
            </TableContainer>
        </Container>
    );
}
